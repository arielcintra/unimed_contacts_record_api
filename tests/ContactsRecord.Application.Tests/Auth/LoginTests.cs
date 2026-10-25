using ContactsRecord.Application.Auth;
using ContactsRecord.Application.Auth.Dtos;
using ContactsRecord.Application.Common.Exceptions;
using ContactsRecord.Domain.Common;
using ContactsRecord.Domain.Users;
using NSubstitute;

namespace ContactsRecord.Application.Tests.Auth;

public class LoginTests
{
    private const string Password = "Admin@123";
    private const string PasswordHash = "hashed";

    private static readonly User StoredUser = User.Create(
        Email.Create("admin@unimed.local"), PasswordHash, new DateTimeOffset(2026, 10, 25, 0, 0, 0, TimeSpan.Zero));

    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly ITokenService _tokens = Substitute.For<ITokenService>();
    private readonly AuthService _service;

    public LoginTests()
    {
        _users.GetByEmailAsync(StoredUser.Email, Arg.Any<CancellationToken>()).Returns(StoredUser);
        _passwordHasher.Verify(PasswordHash, Password).Returns(true);
        _service = new AuthService(_users, _passwordHasher, _tokens);
    }

    [Fact]
    public async Task Valid_credentials_return_a_token()
    {
        var token = new LoginOutputDto("jwt", DateTimeOffset.UtcNow.AddHours(1), UserRole.Common);
        _tokens.Generate(StoredUser).Returns(token);

        var output = await _service.LoginAsync(new LoginInputDto(" ADMIN@unimed.local ", Password), CancellationToken.None);

        Assert.Equal(token, output);
    }

    [Theory]
    [InlineData("admin@unimed.local", "wrong")]   // wrong password
    [InlineData("other@unimed.local", Password)]  // unknown e-mail
    [InlineData("not-an-email", Password)]        // invalid e-mail
    [InlineData("", Password)]
    [InlineData("admin@unimed.local", "")]
    public async Task Invalid_credentials_fail_with_the_same_message(string email, string password)
    {
        var error = await Assert.ThrowsAsync<UnauthorizedException>(
            () => _service.LoginAsync(new LoginInputDto(email, password), CancellationToken.None));

        Assert.Equal(AuthMessages.InvalidCredentials, error.Message);
        _tokens.DidNotReceiveWithAnyArgs().Generate(default!);
    }
}
