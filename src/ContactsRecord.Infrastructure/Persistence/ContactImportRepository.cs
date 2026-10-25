using ContactsRecord.Application.Common.Dtos;
using ContactsRecord.Application.Imports;
using ContactsRecord.Application.Imports.Dtos;
using ContactsRecord.Domain.Imports;
using ContactsRecord.Infrastructure.Persistence.Dtos;

namespace ContactsRecord.Infrastructure.Persistence;

internal sealed class ContactImportRepository(DbSession session) : IContactImportRepository
{
    // Every column except "content": the file is only loaded when it is processed.
    private const string Columns = """
        id, file_name, status, total_rows, imported_rows, duplicate_rows, invalid_rows,
        failure_reason, created_at, started_at, finished_at
        """;

    public Task AddAsync(ContactImport import, byte[] content, CancellationToken cancellationToken) =>
        session.ExecuteCommandAsync($"""
            INSERT INTO contact_imports ({Columns}, content)
            VALUES (@Id, @FileName, @Status, @TotalRows, @ImportedRows, @DuplicateRows, @InvalidRows,
                    @FailureReason, @CreatedAt, @StartedAt, @FinishedAt, @Content)
            """, ContactImportPersistenceDto.FromDomain(import, content), cancellationToken);

    public async Task<ContactImport?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await session.QuerySingleOrDefaultAsync<ContactImportPersistenceDto>(
            $"SELECT {Columns} FROM contact_imports WHERE id = @id", new { id }, cancellationToken);
        return row?.ToDomain();
    }

    public Task<byte[]> GetContentAsync(Guid id, CancellationToken cancellationToken) =>
        session.QuerySingleAsync<byte[]>(
            "SELECT content FROM contact_imports WHERE id = @id", new { id }, cancellationToken);

    public Task UpdateAsync(ContactImport import, CancellationToken cancellationToken) =>
        session.ExecuteCommandAsync("""
            UPDATE contact_imports
            SET status = @Status, total_rows = @TotalRows, imported_rows = @ImportedRows,
                duplicate_rows = @DuplicateRows, invalid_rows = @InvalidRows, failure_reason = @FailureReason,
                started_at = @StartedAt, finished_at = @FinishedAt
            WHERE id = @Id
            """, ContactImportPersistenceDto.FromDomain(import), cancellationToken);

    public async Task<PagedResultDto<ContactImport>> SearchAsync(ImportQueryDto query, CancellationToken cancellationToken)
    {
        var sql = $"""
            SELECT COUNT(*) FROM contact_imports;
            SELECT {Columns} FROM contact_imports
            ORDER BY created_at DESC, id DESC
            LIMIT @PageSize OFFSET @Offset;
            """;

        await using var results = await session.QueryMultipleAsync(
            sql, new { query.PageSize, query.Offset }, cancellationToken);
        var totalCount = await results.ReadSingleAsync<long>();
        var rows = await results.ReadAsync<ContactImportPersistenceDto>();

        return new PagedResultDto<ContactImport>(
            rows.Select(row => row.ToDomain()).ToList(), query.Page, query.PageSize, (int)totalCount);
    }

    public Task AddErrorsAsync(
        Guid importId, IReadOnlyCollection<ImportRowError> errors, CancellationToken cancellationToken)
    {
        if (errors.Count == 0) return Task.CompletedTask;

        // One statement for the whole batch of errors.
        const string sql = """
            INSERT INTO contact_import_errors (import_id, line_number, kind, content, reason)
            SELECT @ImportId, * FROM unnest(@LineNumbers::integer[], @Kinds::text[], @Contents::text[], @Reasons::text[])
            """;

        var parameters = new
        {
            ImportId = importId,
            LineNumbers = errors.Select(e => e.LineNumber).ToArray(),
            Kinds = errors.Select(e => e.Kind.ToString()).ToArray(),
            Contents = errors.Select(e => e.Content).ToArray(),
            Reasons = errors.Select(e => e.Reason).ToArray(),
        };

        return session.ExecuteCommandAsync(sql, parameters, cancellationToken);
    }

    public async Task<PagedResultDto<ImportRowError>> SearchErrorsAsync(
        Guid importId, ImportRowErrorQueryDto query, CancellationToken cancellationToken)
    {
        const string filter = "WHERE import_id = @ImportId AND (@Kind::text IS NULL OR kind = @Kind)";

        var sql = $"""
            SELECT COUNT(*) FROM contact_import_errors {filter};
            SELECT line_number, kind, content, reason FROM contact_import_errors {filter}
            ORDER BY line_number
            LIMIT @PageSize OFFSET @Offset;
            """;

        var parameters = new { ImportId = importId, Kind = query.Kind?.ToString(), query.PageSize, query.Offset };

        await using var results = await session.QueryMultipleAsync(sql, parameters, cancellationToken);
        var totalCount = await results.ReadSingleAsync<long>();
        var rows = await results.ReadAsync<ImportRowErrorPersistenceDto>();

        return new PagedResultDto<ImportRowError>(
            rows.Select(row => row.ToDomain()).ToList(), query.Page, query.PageSize, (int)totalCount);
    }
}
