namespace ContactsRecord.Application.Imports;

/// <summary>The file can't be read as the expected CSV (e.g. missing header column). Fails the whole import.</summary>
public sealed class InvalidCsvException(string message) : Exception(message);
