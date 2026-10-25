using System.Globalization;
using System.Text;
using ContactsRecord.Application.Contacts.Dtos;
using ContactsRecord.Application.Imports;
using CsvHelper;
using CsvHelper.Configuration;

namespace ContactsRecord.Infrastructure.Imports;

/// <summary>
/// Reads "nome,email,telefone" CSV files: UTF-8 (with or without BOM), "," or ";" delimiter,
/// case-insensitive header, quoted fields and blank lines.
/// </summary>
internal sealed class CsvContactReader : ICsvContactReader
{
    private const string NameColumn = "nome";
    private const string EmailColumn = "email";
    private const string PhoneColumn = "telefone";
    private static readonly string[] RequiredColumns = [NameColumn, EmailColumn, PhoneColumn];

    private static readonly CsvConfiguration Configuration = new(CultureInfo.InvariantCulture)
    {
        DetectDelimiter = true,
        DetectDelimiterValues = [",", ";"],
        PrepareHeaderForMatch = args => NormalizeHeader(args.Header),
        TrimOptions = TrimOptions.Trim,
        IgnoreBlankLines = true,
        MissingFieldFound = null,
    };

    public IEnumerable<CsvContactRow> Read(Stream content)
    {
        // detectEncodingFromByteOrderMarks drops the BOM Excel adds to "CSV UTF-8" files.
        using var reader = new StreamReader(content, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        using var csv = new CsvReader(reader, Configuration);

        ReadHeader(csv);

        while (true)
        {
            CsvContactRow row;
            try
            {
                if (!csv.Read()) yield break;
                row = ToRow(csv);
            }
            catch (CsvHelperException ex)
            {
                throw new InvalidCsvException(
                    $"Conteúdo inválido no arquivo, próximo da linha {ex.Context?.Parser?.RawRow}.");
            }

            yield return row;
        }
    }

    private static void ReadHeader(CsvReader csv)
    {
        try
        {
            if (!csv.Read() || !csv.ReadHeader())
                throw new InvalidCsvException("O arquivo não tem cabeçalho.");
        }
        catch (CsvHelperException)
        {
            throw new InvalidCsvException("Não foi possível ler o cabeçalho do arquivo.");
        }

        var columns = (csv.HeaderRecord ?? []).Select(NormalizeHeader).ToHashSet();
        if (!RequiredColumns.All(columns.Contains))
            throw new InvalidCsvException(
                $"Cabeçalho inválido. Colunas obrigatórias: {string.Join(", ", RequiredColumns)}.");
    }

    private static CsvContactRow ToRow(CsvReader csv) => new(
        csv.Parser.RawRow,
        csv.Parser.RawRecord.TrimEnd('\r', '\n'),
        new ContactInputDto(csv.GetField(NameColumn), csv.GetField(EmailColumn), csv.GetField(PhoneColumn)));

    private static string NormalizeHeader(string header) => header.Trim().ToLowerInvariant();
}
