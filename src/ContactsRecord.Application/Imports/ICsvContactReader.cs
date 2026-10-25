using ContactsRecord.Application.Contacts.Dtos;

namespace ContactsRecord.Application.Imports;

/// <param name="LineNumber">Line in the file (the header is line 1).</param>
/// <param name="Content">The row exactly as written in the file, for error reports.</param>
public sealed record CsvContactRow(int LineNumber, string Content, ContactInputDto Contact);

public interface ICsvContactReader
{
    /// <summary>Reads the rows lazily, after validating the header.</summary>
    /// <exception cref="InvalidCsvException">Missing header columns or unreadable content.</exception>
    IEnumerable<CsvContactRow> Read(Stream content);
}
