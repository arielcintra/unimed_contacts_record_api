using ContactsRecord.Application.Common.Dtos;
using ContactsRecord.Application.Contacts;
using ContactsRecord.Application.Contacts.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace ContactsRecord.Api.Controllers;

[ApiController]
[Route("api/contacts")]
public sealed class ContactsController(ContactService contacts) : ControllerBase
{
    /// <summary>Paged listing with search by name, e-mail or phone and favorite/status filters.</summary>
    [HttpGet]
    public Task<PagedResultDto<ContactOutputDto>> Search(
        [FromQuery] ContactQueryDto query, CancellationToken cancellationToken) =>
        contacts.SearchAsync(query, cancellationToken);

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<ActionResult<ContactOutputDto>> Create(
        ContactInputDto input, CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created, await contacts.CreateAsync(input, cancellationToken));

    [HttpPut("{id:guid}")]
    public Task<ContactOutputDto> Update(Guid id, ContactInputDto input, CancellationToken cancellationToken) =>
        contacts.UpdateAsync(id, input, cancellationToken);

    [HttpPatch("{id:guid}/activate")]
    public Task<ContactOutputDto> Activate(Guid id, CancellationToken cancellationToken) =>
        contacts.ActivateAsync(id, cancellationToken);

    [HttpPatch("{id:guid}/deactivate")]
    public Task<ContactOutputDto> Deactivate(Guid id, CancellationToken cancellationToken) =>
        contacts.DeactivateAsync(id, cancellationToken);

    [HttpPut("{id:guid}/favorite")]
    public Task<ContactOutputDto> MarkAsFavorite(Guid id, CancellationToken cancellationToken) =>
        contacts.MarkAsFavoriteAsync(id, cancellationToken);

    [HttpDelete("{id:guid}/favorite")]
    public Task<ContactOutputDto> UnmarkAsFavorite(Guid id, CancellationToken cancellationToken) =>
        contacts.UnmarkAsFavoriteAsync(id, cancellationToken);
}
