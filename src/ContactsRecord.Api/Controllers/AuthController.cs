using ContactsRecord.Application.Auth;
using ContactsRecord.Application.Auth.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ContactsRecord.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(AuthService auth) : ControllerBase
{
    /// <summary>Exchanges e-mail and password for a JWT access token.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public Task<LoginOutputDto> Login(LoginInputDto input, CancellationToken cancellationToken) =>
        auth.LoginAsync(input, cancellationToken);
}
