using ContactsRecord.Application.Auth.Dtos;
using ContactsRecord.Application.Common.Exceptions;
using ContactsRecord.Domain.Common;

namespace ContactsRecord.Application.Auth;

public sealed class AuthService(IUserRepository users, IPasswordHasher passwordHasher, ITokenService tokens)
{
    public async Task<LoginOutputDto> LoginAsync(LoginInputDto input, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(input.Email) || string.IsNullOrEmpty(input.Password))
            throw new UnauthorizedException(AuthMessages.InvalidCredentials);

        Email email;
        try
        {
            email = Email.Create(input.Email);
        }
        catch (DomainException)
        {
            throw new UnauthorizedException(AuthMessages.InvalidCredentials);
        }

        var user = await users.GetByEmailAsync(email, cancellationToken);
        if (user is null || !passwordHasher.Verify(user.PasswordHash, input.Password))
            throw new UnauthorizedException(AuthMessages.InvalidCredentials);

        return tokens.Generate(user);
    }
}
