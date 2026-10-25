using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ContactsRecord.Application.Common.Dtos;
using ContactsRecord.Application.Contacts.Dtos;
using ContactsRecord.Application.Imports.Dtos;
using ContactsRecord.Domain.Imports;
using static ContactsRecord.IntegrationTests.Contacts.ContactTestData;

namespace ContactsRecord.IntegrationTests.Imports;

/// <summary>End to end: upload → RabbitMQ → consumer → PostgreSQL, then status and error queries.</summary>
[Collection(ApiCollection.Name)]
public class ImportsApiTests(ApiFactory factory)
{
    private static readonly TimeSpan ProcessingTimeout = TimeSpan.FromSeconds(30);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly HttpClient _client = factory.CreateAuthenticatedClient();

    [Fact]
    public async Task Imports_valid_rows_and_reports_invalid_and_duplicate_ones()
    {
        var token = UniqueToken();
        var (phone1, phone2, phone3) = (UniquePhone(), UniquePhone(), UniquePhone());
        var csv = $"""
            nome;email;telefone
            Ana {token};ana@example.com;{phone1}
            Bia {token};;{phone2}
            Sem telefone {token};;123
            Ana repetida {token};;{phone1}
            Carla {token};;{phone3}
            """;

        var import = await UploadAndWaitAsync(csv);

        Assert.Equal(ImportStatus.CompletedWithErrors, import.Status);
        Assert.Equal((5, 3, 1, 1), (import.TotalRows, import.ImportedRows, import.DuplicateRows, import.InvalidRows));

        var contacts = await _client.GetFromJsonAsync<PagedResultDto<ContactOutputDto>>($"/api/contacts?search={token}", Json);
        Assert.Equal(3, contacts!.TotalCount);

        var errors = await GetErrorsAsync(import.Id);
        Assert.Collection(errors,
            invalid => Assert.Equal((4, ImportRowErrorKind.Invalid), (invalid.LineNumber, invalid.Kind)),
            duplicate =>
            {
                Assert.Equal((5, ImportRowErrorKind.Duplicate), (duplicate.LineNumber, duplicate.Kind));
                Assert.Contains("linha 2", duplicate.Reason);
            });
    }

    [Fact]
    public async Task Reprocessing_the_same_file_creates_no_duplicates()
    {
        var token = UniqueToken();
        var csv = $"""
            nome,email,telefone
            Ana {token},,{UniquePhone()}
            Bia {token},,{UniquePhone()}
            Carla {token},,{UniquePhone()}
            """;

        var first = await UploadAndWaitAsync(csv);
        var second = await UploadAndWaitAsync(csv);

        Assert.Equal((ImportStatus.Completed, 3), (first.Status, first.ImportedRows));
        Assert.Equal((ImportStatus.CompletedWithErrors, 0, 3), (second.Status, second.ImportedRows, second.DuplicateRows));

        var contacts = await _client.GetFromJsonAsync<PagedResultDto<ContactOutputDto>>($"/api/contacts?search={token}", Json);
        Assert.Equal(3, contacts!.TotalCount);

        var duplicates = await GetErrorsAsync(second.Id, "?kind=Duplicate");
        Assert.Equal(3, duplicates.Count);
        Assert.All(duplicates, d => Assert.Contains($"já cadastrado para o contato \"", d.Reason));
    }

    [Fact]
    public async Task File_with_invalid_header_fails_with_reason()
    {
        var import = await UploadAndWaitAsync("name,phone\nAna,11987654321");

        Assert.Equal(ImportStatus.Failed, import.Status);
        Assert.Contains("Cabeçalho inválido", import.FailureReason);
    }

    [Fact]
    public async Task Upload_of_non_csv_file_returns_400()
    {
        var response = await UploadAsync("nome,email,telefone", fileName: "contatos.txt");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Upload_without_token_returns_401()
    {
        using var content = CsvContent("nome,email,telefone", "contatos.csv");

        var response = await factory.CreateClient().PostAsync("/api/imports", content);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<ContactImportOutputDto> UploadAndWaitAsync(string csv)
    {
        var response = await UploadAsync(csv);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var import = (await response.Content.ReadFromJsonAsync<ContactImportOutputDto>(Json))!;

        // Polls the status endpoint, as a client would, until the consumer finishes the import.
        var deadline = DateTime.UtcNow + ProcessingTimeout;
        while (import.Status is ImportStatus.Pending or ImportStatus.Processing)
        {
            Assert.True(DateTime.UtcNow < deadline, "Import was not processed in time.");
            await Task.Delay(200);
            import = (await _client.GetFromJsonAsync<ContactImportOutputDto>(response.Headers.Location, Json))!;
        }

        return import;
    }

    private Task<HttpResponseMessage> UploadAsync(string csv, string fileName = "contatos.csv")
    {
        var content = CsvContent(csv, fileName);
        return _client.PostAsync("/api/imports", content);
    }

    private async Task<IReadOnlyList<ImportRowErrorOutputDto>> GetErrorsAsync(Guid importId, string query = "")
    {
        var page = await _client.GetFromJsonAsync<PagedResultDto<ImportRowErrorOutputDto>>(
            $"/api/imports/{importId}/errors{query}", Json);
        return page!.Items;
    }

    private static MultipartFormDataContent CsvContent(string csv, string fileName) => new()
    {
        { new ByteArrayContent(Encoding.UTF8.GetBytes(csv)), "file", fileName },
    };
}
