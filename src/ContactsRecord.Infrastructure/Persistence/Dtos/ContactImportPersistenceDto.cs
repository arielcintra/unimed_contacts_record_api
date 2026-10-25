using ContactsRecord.Domain.Imports;

namespace ContactsRecord.Infrastructure.Persistence.Dtos;

/// <summary>
/// Row of the "contact_imports" table: EF Core builds the schema from it and Dapper reads/writes it.
/// Never leaves the Infrastructure layer.
/// </summary>
public sealed class ContactImportPersistenceDto
{
    public Guid Id { get; init; }
    public string FileName { get; init; } = null!;

    // Stored as text ("Pending", "Completed"...) so the table is readable without the code.
    public string Status { get; init; } = null!;

    /// <summary>Uploaded file. Only written on insert and read by the processor; listings never select it.</summary>
    public byte[] Content { get; init; } = null!;

    public int TotalRows { get; init; }
    public int ImportedRows { get; init; }
    public int DuplicateRows { get; init; }
    public int InvalidRows { get; init; }
    public string? FailureReason { get; init; }

    // DateTime (UTC) instead of DateTimeOffset: it is what Npgsql returns for timestamptz.
    public DateTime CreatedAt { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? FinishedAt { get; init; }

    public static ContactImportPersistenceDto FromDomain(ContactImport import, byte[]? content = null) => new()
    {
        Id = import.Id,
        FileName = import.FileName,
        Status = import.Status.ToString(),
        Content = content ?? [],
        TotalRows = import.TotalRows,
        ImportedRows = import.ImportedRows,
        DuplicateRows = import.DuplicateRows,
        InvalidRows = import.InvalidRows,
        FailureReason = import.FailureReason,
        CreatedAt = import.CreatedAt.UtcDateTime,
        StartedAt = import.StartedAt?.UtcDateTime,
        FinishedAt = import.FinishedAt?.UtcDateTime,
    };

    public ContactImport ToDomain() => ContactImport.Restore(
        Id,
        FileName,
        Enum.Parse<ImportStatus>(Status),
        TotalRows,
        ImportedRows,
        DuplicateRows,
        InvalidRows,
        FailureReason,
        AsUtc(CreatedAt),
        StartedAt is null ? null : AsUtc(StartedAt.Value),
        FinishedAt is null ? null : AsUtc(FinishedAt.Value));

    private static DateTimeOffset AsUtc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
