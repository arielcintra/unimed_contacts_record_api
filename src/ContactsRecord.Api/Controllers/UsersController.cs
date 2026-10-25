using ContactsRecord.Application.Common.Dtos;
using ContactsRecord.Application.Users;
using ContactsRecord.Application.Users.Dtos;
using ContactsRecord.Domain.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ContactsRecord.Api.Controllers;

/// <summary>User management: administrator only (common users get 403).</summary>
[ApiController]
[Route("api/users")]
[Authorize(Roles = nameof(UserRole.Admin))]
public sealed class UsersController(UserService users) : ControllerBase
{
    /// <summary>Paged listing with search by e-mail; the administrator comes first.</summary>
    [HttpGet]
    public Task<PagedResultDto<UserOutputDto>> Search(
        [FromQuery] UserQueryDto query, CancellationToken cancellationToken) =>
        users.SearchAsync(query, cancellationToken);

    /// <summary>Registers a common user.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<ActionResult<UserOutputDto>> Create(UserInputDto input, CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created, await users.CreateAsync(input, cancellationToken));

    /// <summary>Changes e-mail and, when given, the password. The administrator can't be edited.</summary>
    [HttpPut("{id:guid}")]
    public Task<UserOutputDto> Update(Guid id, UserInputDto input, CancellationToken cancellationToken) =>
        users.UpdateAsync(id, input, cancellationToken);
}
