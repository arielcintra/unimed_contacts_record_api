using ContactsRecord.Application.Auth;
using ContactsRecord.Application.Common.Exceptions;
using ContactsRecord.Application.Users;
using ContactsRecord.Application.Users.Dtos;
using ContactsRecord.Domain.Common;
using ContactsRecord.Domain.Users;
using NSubstitute;

namespace ContactsRecord.Application.Tests.Users;

public class UserServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly UserService _service;

    public UserServiceTests()
    {
        _passwordHasher.Hash(Arg.Any<string>()).Returns(call => $"hash:{call.Arg<string>()}");
        var clock = Substitute.For<TimeProvider>();
        clock.GetUtcNow().Returns(Now);
        _service = new UserService(_users, _passwordHasher, clock);
    }

    [Fact]
    public async Task Create_registers_a_common_user_with_hashed_password()
    {
        var output = await _service.CreateAsync(new UserInputDto(" Bia@Unimed.Local ", "Senha@123"), CancellationToken.None);

        Assert.Equal("bia@unimed.local", output.Email);
        Assert.Equal(UserRole.Common, output.Role);
        await _users.Received(1).AddAsync(
            Arg.Is<User>(u => u.Id == output.Id && u.PasswordHash == "hash:Senha@123" && !u.IsAdmin),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("not-an-email", "Senha@123")]
    [InlineData("bia@unimed.local", "curta")]
    [InlineData("bia@unimed.local", null)]
    public async Task Create_rejects_invalid_data_without_touching_the_repository(string email, string? password)
    {
        await Assert.ThrowsAsync<DomainException>(
            () => _service.CreateAsync(new UserInputDto(email, password), CancellationToken.None));

        Assert.Empty(_users.ReceivedCalls());
    }

    [Fact]
    public async Task Create_rejects_email_of_another_user()
    {
        GivenStoredUser("bia@unimed.local");

        var error = await Assert.ThrowsAsync<ConflictException>(
            () => _service.CreateAsync(new UserInputDto("bia@unimed.local", "Senha@123"), CancellationToken.None));

        Assert.Equal(UserMessages.EmailInUse, error.Message);
        await _users.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task Update_changes_email_and_keeps_password_when_blank()
    {
        var user = GivenStoredUser("bia@unimed.local");

        var output = await _service.UpdateAsync(user.Id, new UserInputDto("beatriz@unimed.local", ""), CancellationToken.None);

        Assert.Equal("beatriz@unimed.local", output.Email);
        await _users.Received(1).UpdateAsync(
            Arg.Is<User>(u => u.Id == user.Id && u.PasswordHash == "stored-hash"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_replaces_password_when_given()
    {
        var user = GivenStoredUser("bia@unimed.local");

        await _service.UpdateAsync(user.Id, new UserInputDto(user.Email.Value, "NovaSenha@1"), CancellationToken.None);

        await _users.Received(1).UpdateAsync(
            Arg.Is<User>(u => u.PasswordHash == "hash:NovaSenha@1"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_keeps_own_email_without_conflict()
    {
        var user = GivenStoredUser("bia@unimed.local");

        await _service.UpdateAsync(user.Id, new UserInputDto("bia@unimed.local", null), CancellationToken.None);

        await _users.Received(1).UpdateAsync(user, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_rejects_email_of_another_user()
    {
        var user = GivenStoredUser("bia@unimed.local");
        GivenStoredUser("carla@unimed.local");

        await Assert.ThrowsAsync<ConflictException>(
            () => _service.UpdateAsync(user.Id, new UserInputDto("carla@unimed.local", null), CancellationToken.None));

        await _users.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
    }

    [Fact]
    public async Task Update_refuses_the_administrator()
    {
        var admin = GivenStoredUser("admin@unimed.local", UserRole.Admin);

        await Assert.ThrowsAsync<DomainException>(
            () => _service.UpdateAsync(admin.Id, new UserInputDto("outro@unimed.local", null), CancellationToken.None));

        await _users.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
    }

    [Fact]
    public async Task Update_of_unknown_user_fails_with_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.UpdateAsync(Guid.NewGuid(), new UserInputDto("bia@unimed.local", null), CancellationToken.None));
    }

    /// <summary>A user already in the repository, found by id and by e-mail.</summary>
    private User GivenStoredUser(string email, UserRole role = UserRole.Common)
    {
        var user = User.Restore(Guid.NewGuid(), Email.Create(email), "stored-hash", role, Now.AddDays(-1));
        _users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        _users.GetByEmailAsync(user.Email, Arg.Any<CancellationToken>()).Returns(user);
        return user;
    }
}
