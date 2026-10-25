using ContactsRecord.Domain.Common;

namespace ContactsRecord.Domain.Imports;

/// <summary>A CSV file sent for import and the progress of its asynchronous processing.</summary>
public sealed class ContactImport : Entity
{
    public const int FileNameMaxLength = 255;
    public const long MaxFileSizeBytes = 5 * 1024 * 1024;
    private const string CsvExtension = ".csv";

    public string FileName { get; private set; } = null!;
    public ImportStatus Status { get; private set; }
    public int TotalRows { get; private set; }
    public int ImportedRows { get; private set; }
    public int DuplicateRows { get; private set; }
    public int InvalidRows { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? FinishedAt { get; private set; }

    public bool IsFinished => Status is ImportStatus.Completed or ImportStatus.CompletedWithErrors or ImportStatus.Failed;

    private ContactImport() { }

    public static ContactImport Create(string? fileName, long sizeInBytes, DateTimeOffset now)
    {
        var name = fileName?.Trim() ?? string.Empty;

        if (name.Length == 0 || name.Length > FileNameMaxLength)
            throw new DomainException("Nome do arquivo inválido.");
        if (!name.EndsWith(CsvExtension, StringComparison.OrdinalIgnoreCase))
            throw new DomainException("O arquivo deve ter a extensão .csv.");
        if (sizeInBytes <= 0)
            throw new DomainException("O arquivo está vazio.");
        if (sizeInBytes > MaxFileSizeBytes)
            throw new DomainException($"O arquivo excede o limite de {MaxFileSizeBytes / (1024 * 1024)} MB.");

        return new ContactImport
        {
            Id = Guid.CreateVersion7(now),
            FileName = name,
            Status = ImportStatus.Pending,
            CreatedAt = now,
        };
    }

    /// <summary>Rebuilds an import already persisted; does not apply creation rules.</summary>
    public static ContactImport Restore(
        Guid id,
        string fileName,
        ImportStatus status,
        int totalRows,
        int importedRows,
        int duplicateRows,
        int invalidRows,
        string? failureReason,
        DateTimeOffset createdAt,
        DateTimeOffset? startedAt,
        DateTimeOffset? finishedAt) =>
        new()
        {
            Id = id,
            FileName = fileName,
            Status = status,
            TotalRows = totalRows,
            ImportedRows = importedRows,
            DuplicateRows = duplicateRows,
            InvalidRows = invalidRows,
            FailureReason = failureReason,
            CreatedAt = createdAt,
            StartedAt = startedAt,
            FinishedAt = finishedAt,
        };

    /// <summary>
    /// Starts the processing, or resumes it after an interruption. Each batch is saved atomically
    /// together with the counters, so <see cref="TotalRows"/> is exactly the number of rows already
    /// processed and a resumed import continues from there, keeping counters and errors.
    /// </summary>
    public void Start(DateTimeOffset now)
    {
        if (Status is not (ImportStatus.Pending or ImportStatus.Processing))
            throw new DomainException($"Não é possível iniciar uma importação com status {Status}.");

        if (Status == ImportStatus.Pending) StartedAt = now;
        Status = ImportStatus.Processing;
    }

    public void RecordBatch(int imported, int duplicates, int invalid)
    {
        EnsureProcessing();

        ImportedRows += imported;
        DuplicateRows += duplicates;
        InvalidRows += invalid;
        TotalRows += imported + duplicates + invalid;
    }

    public void Complete(DateTimeOffset now)
    {
        EnsureProcessing();

        Status = DuplicateRows + InvalidRows == 0 ? ImportStatus.Completed : ImportStatus.CompletedWithErrors;
        FinishedAt = now;
    }

    public void Fail(string reason, DateTimeOffset now)
    {
        if (IsFinished)
            throw new DomainException($"Não é possível marcar como falha uma importação com status {Status}.");

        Status = ImportStatus.Failed;
        FailureReason = reason;
        FinishedAt = now;
    }

    private void EnsureProcessing()
    {
        if (Status != ImportStatus.Processing)
            throw new DomainException($"A importação não está em processamento (status {Status}).");
    }
}
