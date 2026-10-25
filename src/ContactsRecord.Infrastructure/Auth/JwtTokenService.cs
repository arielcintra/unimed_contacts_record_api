using System.Security.Claims;
using ContactsRecord.Application.Auth;
using ContactsRecord.Application.Auth.Dtos;
using ContactsRecord.Domain.Users;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ContactsRecord.Infrastructure.Auth;

internal sealed class JwtTokenService(IOptions<JwtOptions> options, TimeProvider clock) : ITokenService
{
    /// <summary>Claim read by [Authorize(Roles = ...)]; the bearer validation maps it as the role claim.</summary>
    public const string RoleClaim = "role";

    private readonly JsonWebTokenHandler _handler = new();

    public LoginOutputDto Generate(User user)
    {
        var jwt = options.Value;
        var now = clock.GetUtcNow();
        var expiresAt = now.AddMinutes(jwt.ExpirationMinutes);

        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email.Value),
                new Claim(RoleClaim, user.Role.ToString()),
            ]),
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(jwt.SigningKey(), SecurityAlgorithms.HmacSha256),
        });

        return new LoginOutputDto(token, expiresAt, user.Role);
    }
}
