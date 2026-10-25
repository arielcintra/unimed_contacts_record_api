using ContactsRecord.Application.Common.Dtos;
using ContactsRecord.Application.Common.Exceptions;
using ContactsRecord.Application.Contacts;
using ContactsRecord.Application.Contacts.Dtos;
using ContactsRecord.Domain.Contacts;
using ContactsRecord.Infrastructure.Persistence.Dtos;
using Npgsql;

namespace ContactsRecord.Infrastructure.Persistence;

internal sealed class ContactRepository(DbSession session) : IContactRepository
{
    private const string Columns =
        "id, name, email, phone, is_favorite, is_active, created_at, updated_at";

    public Task<Contact?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        QuerySingleAsync($"SELECT {Columns} FROM contacts WHERE id = @id", new { id }, cancellationToken);

    public Task<Contact?> GetByPhoneAsync(PhoneNumber phone, CancellationToken cancellationToken) =>
        QuerySingleAsync($"SELECT {Columns} FROM contacts WHERE phone = @phone", new { phone = phone.Value }, cancellationToken);

    public async Task<IReadOnlyList<Contact>> GetByPhonesAsync(
        IReadOnlyCollection<PhoneNumber> phones, CancellationToken cancellationToken)
    {
        if (phones.Count == 0) return [];

        var rows = await session.QueryAsync<ContactPersistenceDto>(
            $"SELECT {Columns} FROM contacts WHERE phone = ANY(@phones)",
            new { phones = phones.Select(p => p.Value).ToArray() },
            cancellationToken);
        return rows.Select(row => row.ToDomain()).ToList();
    }

    public async Task<IReadOnlySet<string>> AddManyIgnoringDuplicatesAsync(
        IReadOnlyCollection<Contact> contacts, CancellationToken cancellationToken)
    {
        if (contacts.Count == 0) return new HashSet<string>();

        // One statement for the whole batch. ON CONFLICT (phone) only skips phone duplicates (any other
        // violation still fails) and RETURNING reports which rows were actually inserted.
        const string sql = $"""
            INSERT INTO contacts ({Columns})
            SELECT * FROM unnest(
                @Ids::uuid[], @Names::text[], @Emails::text[], @Phones::text[],
                @IsFavorites::boolean[], @IsActives::boolean[], @CreatedAts::timestamptz[], @UpdatedAts::timestamptz[])
            ON CONFLICT (phone) DO NOTHING
            RETURNING phone
            """;

        var rows = contacts.Select(ContactPersistenceDto.FromDomain).ToList();
        var parameters = new
        {
            Ids = rows.Select(r => r.Id).ToArray(),
            Names = rows.Select(r => r.Name).ToArray(),
            Emails = rows.Select(r => r.Email).ToArray(),
            Phones = rows.Select(r => r.Phone).ToArray(),
            IsFavorites = rows.Select(r => r.IsFavorite).ToArray(),
            IsActives = rows.Select(r => r.IsActive).ToArray(),
            CreatedAts = rows.Select(r => r.CreatedAt).ToArray(),
            UpdatedAts = rows.Select(r => r.UpdatedAt).ToArray(),
        };

        var inserted = await session.QueryAsync<string>(sql, parameters, cancellationToken);
        return inserted.ToHashSet();
    }

    public Task AddAsync(Contact contact, CancellationToken cancellationToken) =>
        WriteAsync($"INSERT INTO contacts ({Columns}) " +
            "VALUES (@Id, @Name, @Email, @Phone, @IsFavorite, @IsActive, @CreatedAt, @UpdatedAt)",
            contact, cancellationToken);

    public Task UpdateAsync(Contact contact, CancellationToken cancellationToken) =>
        WriteAsync("""
            UPDATE contacts
            SET name = @Name, email = @Email, phone = @Phone,
                is_favorite = @IsFavorite, is_active = @IsActive, updated_at = @UpdatedAt
            WHERE id = @Id
            """, contact, cancellationToken);

    public async Task<PagedResultDto<Contact>> SearchAsync(ContactQueryDto query, CancellationToken cancellationToken)
    {
        const string filter = """
            WHERE (@Pattern::text IS NULL
                   OR name ILIKE @Pattern
                   OR email ILIKE @Pattern
                   OR phone LIKE @PhonePattern)
              AND (@IsFavorite::boolean IS NULL OR is_favorite = @IsFavorite)
              AND (@IsActive::boolean IS NULL OR is_active = @IsActive)
            """;

        var sql = $"""
            SELECT COUNT(*) FROM contacts {filter};
            SELECT {Columns} FROM contacts {filter}
            ORDER BY name, id
            LIMIT @PageSize OFFSET @Offset;
            """;

        var parameters = new
        {
            Pattern = query.Search is null ? null : $"%{EscapeLike(query.Search)}%",
            PhonePattern = PhonePatternOf(query.Search),
            query.IsFavorite,
            query.IsActive,
            query.PageSize,
            query.Offset,
        };

        await using var results = await session.QueryMultipleAsync(sql, parameters, cancellationToken);
        var totalCount = await results.ReadSingleAsync<long>();
        var rows = await results.ReadAsync<ContactPersistenceDto>();

        return new PagedResultDto<Contact>(
            rows.Select(row => row.ToDomain()).ToList(), query.Page, query.PageSize, (int)totalCount);
    }

    private async Task<Contact?> QuerySingleAsync(string sql, object parameters, CancellationToken cancellationToken)
    {
        var row = await session.QuerySingleOrDefaultAsync<ContactPersistenceDto>(sql, parameters, cancellationToken);
        return row?.ToDomain();
    }

    private async Task WriteAsync(string sql, Contact contact, CancellationToken cancellationToken)
    {
        try
        {
            await session.ExecuteCommandAsync(sql, ContactPersistenceDto.FromDomain(contact), cancellationToken);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            // The unique index on "phone" is the final guard against concurrent duplicates.
            throw new ConflictException(ContactMessages.PhoneInUse);
        }
    }

    // Searching by phone only makes sense for terms made of digits and phone punctuation.
    private static string? PhonePatternOf(string? search)
    {
        if (search is null || search.Any(char.IsLetter)) return null;

        var digits = new string(search.Where(char.IsAsciiDigit).ToArray());
        return digits.Length == 0 ? null : $"%{digits}%";
    }

    private static string EscapeLike(string value) =>
        value.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");
}
