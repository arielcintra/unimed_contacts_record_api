using ContactsRecord.Application.Auth;
using ContactsRecord.Application.Common.Dtos;
using ContactsRecord.Application.Common.Exceptions;
using ContactsRecord.Application.Users;
using ContactsRecord.Application.Users.Dtos;
using ContactsRecord.Domain.Common;
using ContactsRecord.Domain.Users;
using ContactsRecord.Infrastructure.Persistence.Dtos;
using Npgsql;

namespace ContactsRecord.Infrastructure.Persistence;

internal sealed class UserRepository(DbSession session) : IUserRepository
{
    private const string Columns = "id, email, password_hash, role, created_at";

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        QuerySingleAsync($"SELECT {Columns} FROM users WHERE id = @id", new { id }, cancellationToken);

    public Task<User?> GetByEmailAsync(Email email, CancellationToken cancellationToken) =>
        QuerySingleAsync($"SELECT {Columns} FROM users WHERE email = @email", new { email = email.Value }, cancellationToken);

    public Task AddAsync(User user, CancellationToken cancellationToken) =>
        WriteAsync($"INSERT INTO users ({Columns}) VALUES (@Id, @Email, @PasswordHash, @Role, @CreatedAt)",
            user, cancellationToken);

    public Task UpdateAsync(User user, CancellationToken cancellationToken) =>
        WriteAsync("UPDATE users SET email = @Email, password_hash = @PasswordHash, role = @Role WHERE id = @Id",
            user, cancellationToken);

    public async Task EnsureSingleAdminAsync(User admin, CancellationToken cancellationToken)
    {
        var row = UserPersistenceDto.FromDomain(admin);
        var parameters = new
        {
            row.Id,
            row.Email,
            row.PasswordHash,
            row.CreatedAt,
            Admin = nameof(UserRole.Admin),
            Common = nameof(UserRole.Common),
        };

        // ON CONFLICT keeps the startup safe when several instances run it at the same time.
        await session.ExecuteCommandAsync($"""
            INSERT INTO users ({Columns}) VALUES (@Id, @Email, @PasswordHash, @Admin, @CreatedAt)
            ON CONFLICT (email) DO UPDATE SET role = @Admin
            """, parameters, cancellationToken);
        await session.ExecuteCommandAsync(
            "UPDATE users SET role = @Common WHERE role = @Admin AND email <> @Email",
            parameters, cancellationToken);
    }

    public async Task<PagedResultDto<User>> SearchAsync(UserQueryDto query, CancellationToken cancellationToken)
    {
        const string filter = "WHERE (@Pattern::text IS NULL OR email ILIKE @Pattern)";

        // The administrator always comes first, then alphabetical order.
        var sql = $"""
            SELECT COUNT(*) FROM users {filter};
            SELECT {Columns} FROM users {filter}
            ORDER BY role = @Admin DESC, email
            LIMIT @PageSize OFFSET @Offset;
            """;

        var parameters = new
        {
            Pattern = query.Search is null ? null : $"%{EscapeLike(query.Search)}%",
            Admin = nameof(UserRole.Admin),
            query.PageSize,
            query.Offset,
        };

        await using var results = await session.QueryMultipleAsync(sql, parameters, cancellationToken);
        var totalCount = await results.ReadSingleAsync<long>();
        var rows = await results.ReadAsync<UserPersistenceDto>();

        return new PagedResultDto<User>(
            rows.Select(row => row.ToDomain()).ToList(), query.Page, query.PageSize, (int)totalCount);
    }

    private async Task<User?> QuerySingleAsync(string sql, object parameters, CancellationToken cancellationToken)
    {
        var row = await session.QuerySingleOrDefaultAsync<UserPersistenceDto>(sql, parameters, cancellationToken);
        return row?.ToDomain();
    }

    private async Task WriteAsync(string sql, User user, CancellationToken cancellationToken)
    {
        try
        {
            await session.ExecuteCommandAsync(sql, UserPersistenceDto.FromDomain(user), cancellationToken);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            // The unique index on "email" is the final guard against concurrent duplicates.
            throw new ConflictException(UserMessages.EmailInUse);
        }
    }

    private static string EscapeLike(string value) =>
        value.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");
}
