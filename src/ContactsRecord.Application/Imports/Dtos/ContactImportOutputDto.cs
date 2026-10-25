using ContactsRecord.Domain.Imports;

namespace ContactsRecord.Application.Imports.Dtos;

public sealed record ContactImportOutputDto(
    Guid Id,
    string FileName,
    ImportStatus Status,
    int TotalRows,
    int ImportedRows,
    int DuplicateRows,
    int InvalidRows,
    string? FailureReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt)
{
    public static ContactImportOutputDto From(ContactImport import) => new(
        import.Id,
        import.FileName,
        import.Status,
        import.TotalRows,
        import.ImportedRows,
        import.DuplicateRows,
        import.InvalidRows,
        import.FailureReason,
        import.CreatedAt,
        import.StartedAt,
        import.FinishedAt);
}
