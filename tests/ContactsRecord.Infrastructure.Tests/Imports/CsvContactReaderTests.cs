using System.Text;
using ContactsRecord.Application.Imports;
using ContactsRecord.Infrastructure.Imports;

namespace ContactsRecord.Infrastructure.Tests.Imports;

public class CsvContactReaderTests
{
    private readonly CsvContactReader _reader = new();

    private List<CsvContactRow> Read(string csv, bool withBom = false)
    {
        var bytes = Encoding.UTF8.GetBytes(csv);
        if (withBom) bytes = [.. Encoding.UTF8.GetPreamble(), .. bytes];
        return _reader.Read(new MemoryStream(bytes)).ToList();
    }

    [Fact]
    public void Reads_comma_separated_rows_with_line_numbers_and_original_content()
    {
        var rows = Read("nome,email,telefone\nAna,ana@example.com,(11) 98765-4321\nBia,,1134567890\n");

        Assert.Equal(2, rows.Count);
        Assert.Equal((2, "Ana,ana@example.com,(11) 98765-4321"), (rows[0].LineNumber, rows[0].Content));
        Assert.Equal(("Ana", "ana@example.com", "(11) 98765-4321"),
            (rows[0].Contact.Name, rows[0].Contact.Email, rows[0].Contact.Phone));
        Assert.Equal(3, rows[1].LineNumber);
        Assert.True(string.IsNullOrEmpty(rows[1].Contact.Email));
    }

    [Fact]
    public void Detects_semicolon_delimiter()
    {
        var row = Assert.Single(Read("nome;email;telefone\nAna;ana@example.com;11987654321"));

        Assert.Equal(("Ana", "11987654321"), (row.Contact.Name, row.Contact.Phone));
    }

    [Fact]
    public void Ignores_utf8_bom()
    {
        var row = Assert.Single(Read("nome,email,telefone\nAna,,11987654321", withBom: true));

        Assert.Equal("Ana", row.Contact.Name);
    }

    [Fact]
    public void Header_is_case_insensitive_and_column_order_is_free()
    {
        var row = Assert.Single(Read(" Telefone ,NOME,Email\n11987654321,Ana,"));

        Assert.Equal(("Ana", "11987654321"), (row.Contact.Name, row.Contact.Phone));
    }

    [Fact]
    public void Supports_quoted_fields_containing_the_delimiter()
    {
        var row = Assert.Single(Read("nome,email,telefone\n\"Silva, Ana\",,11987654321"));

        Assert.Equal("Silva, Ana", row.Contact.Name);
    }

    [Fact]
    public void Skips_blank_lines()
    {
        var rows = Read("nome,email,telefone\n\nAna,,11987654321\n\n");

        Assert.Equal(3, Assert.Single(rows).LineNumber);
    }

    [Fact]
    public void Missing_values_are_read_as_empty()
    {
        var row = Assert.Single(Read("nome,email,telefone\nAna"));

        Assert.True(string.IsNullOrEmpty(row.Contact.Phone));
    }

    [Theory]
    [InlineData("nome,telefone\nAna,11987654321")]
    [InlineData("name,email,phone\nAna,,11987654321")]
    public void Missing_required_columns_fail(string csv)
    {
        var error = Assert.Throws<InvalidCsvException>(() => Read(csv));

        Assert.Contains("nome, email, telefone", error.Message);
    }

    [Fact]
    public void Empty_file_fails()
    {
        Assert.Throws<InvalidCsvException>(() => Read(""));
    }
}
