using System.ComponentModel.DataAnnotations;

namespace ContactsRecord.Application.Imports;

/// <summary>"Imports" configuration section. Overridable by environment variables (e.g. Imports__BatchSize).</summary>
public sealed class ImportOptions
{
    public const string SectionName = "Imports";

    /// <summary>Rows inserted per database round trip; progress is saved after each batch.</summary>
    [Range(1, 10_000)]
    public int BatchSize { get; init; } = 500;
}
