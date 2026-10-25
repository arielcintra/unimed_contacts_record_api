using ContactsRecord.Domain.Imports;

namespace ContactsRecord.Infrastructure.Persistence.Dtos;

/// <summary>
/// Row of the "contact_import_errors" table: EF Core builds the schema from it and Dapper reads/writes it.
/// Never leaves the Infrastructure layer.
/// </summary>
public sealed class ImportRowErrorPersistenceDto
{
    public long Id { get; init; }
    public Guid ImportId { get; init; }
    public int LineNumber { get; init; }

    // Stored as text ("Invalid" / "Duplicate") so the table is readable without the code.
    public string Kind { get; init; } = null!;

    public string Content { get; init; } = null!;
    public string Reason { get; init; } = null!;

    public ImportRowError ToDomain() => new(LineNumber, Enum.Parse<ImportRowErrorKind>(Kind), Content, Reason);
}
