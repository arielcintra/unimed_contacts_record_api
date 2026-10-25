namespace ContactsRecord.Domain.Imports;

public enum ImportRowErrorKind
{
    /// <summary>The row data breaks a contact rule (e.g. invalid phone).</summary>
    Invalid,

    /// <summary>The phone already exists in the database or earlier in the same file.</summary>
    Duplicate,
}

/// <summary>A CSV row that was not imported, with the original content and the reason.</summary>
public sealed record ImportRowError(int LineNumber, ImportRowErrorKind Kind, string Content, string Reason);
