using ContactsRecord.Application.Common.Dtos;
using ContactsRecord.Application.Imports;
using ContactsRecord.Application.Imports.Dtos;
using ContactsRecord.Domain.Imports;
using Microsoft.AspNetCore.Mvc;

namespace ContactsRecord.Api.Controllers;

[ApiController]
[Route("api/imports")]
public sealed class ContactImportsController(ContactImportService imports) : ControllerBase
{
    // Room for the multipart envelope around a file at the size limit.
    private const long RequestSizeLimit = ContactImport.MaxFileSizeBytes + 64 * 1024;

    /// <summary>Uploads a CSV file (columns nome, email, telefone) and queues it for asynchronous processing.</summary>
    [HttpPost]
    [RequestSizeLimit(RequestSizeLimit)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<ActionResult<ContactImportOutputDto>> Upload(IFormFile file, CancellationToken cancellationToken)
    {
        await using var content = file.OpenReadStream();
        var import = await imports.StartAsync(file.FileName, file.Length, content, cancellationToken);

        // 202: accepted for processing; Location points to the status endpoint.
        return AcceptedAtAction(nameof(Get), new { id = import.Id }, import);
    }

    /// <summary>Import history, most recent first.</summary>
    [HttpGet]
    public Task<PagedResultDto<ContactImportOutputDto>> Search(
        [FromQuery] ImportQueryDto query, CancellationToken cancellationToken) =>
        imports.SearchAsync(query, cancellationToken);

    /// <summary>Status and counters of an import.</summary>
    [HttpGet("{id:guid}")]
    public Task<ContactImportOutputDto> Get(Guid id, CancellationToken cancellationToken) =>
        imports.GetAsync(id, cancellationToken);

    /// <summary>Rows not imported (Invalid or Duplicate), with line number, original content and reason.</summary>
    [HttpGet("{id:guid}/errors")]
    public Task<PagedResultDto<ImportRowErrorOutputDto>> SearchErrors(
        Guid id, [FromQuery] ImportRowErrorQueryDto query, CancellationToken cancellationToken) =>
        imports.SearchErrorsAsync(id, query, cancellationToken);
}
