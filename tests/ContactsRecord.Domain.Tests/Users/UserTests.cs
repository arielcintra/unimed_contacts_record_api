using ContactsRecord.Domain.Common;
using ContactsRecord.Domain.Users;

namespace ContactsRecord.Domain.Tests.Users;

public class UserTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_keeps_normalized_email_and_hash()
    {
        var user = User.Create(Email.Create(" Admin@Unimed.Local "), "hash", Now);

        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal("admin@unimed.local", user.Email.Value);
        Assert.Equal("hash", user.PasswordHash);
        Assert.Equal(Now, user.CreatedAt);
    }

    [Fact]
    public void Create_registers_a_common_user_and_CreateAdmin_an_administrator()
    {
        Assert.Equal(UserRole.Common, User.Create(Email.Create("bia@unimed.local"), "hash", Now).Role);
        Assert.True(User.CreateAdmin(Email.Create("admin@unimed.local"), "hash", Now).IsAdmin);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_requires_password_hash(string hash)
    {
        Assert.Throws<DomainException>(() => User.Create(Email.Create("admin@unimed.local"), hash, Now));
    }

    [Fact]
    public void Create_requires_email()
    {
        Assert.Throws<DomainException>(() => User.Create(null!, "hash", Now));
    }

    [Fact]
    public void Update_changes_email_and_keeps_password_when_no_new_hash_is_given()
    {
        var user = User.Create(Email.Create("bia@unimed.local"), "hash", Now);

        user.Update(Email.Create("beatriz@unimed.local"), passwordHash: null);

        Assert.Equal("beatriz@unimed.local", user.Email.Value);
        Assert.Equal("hash", user.PasswordHash);
    }

    [Fact]
    public void Update_replaces_password_hash()
    {
        var user = User.Create(Email.Create("bia@unimed.local"), "hash", Now);

        user.Update(user.Email, "new-hash");

        Assert.Equal("new-hash", user.PasswordHash);
    }

    [Fact]
    public void Administrator_cannot_be_updated()
    {
        var admin = User.CreateAdmin(Email.Create("admin@unimed.local"), "hash", Now);

        Assert.Throws<DomainException>(() => admin.Update(Email.Create("other@unimed.local"), null));
        Assert.Equal("admin@unimed.local", admin.Email.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1234567")]
    public void Password_shorter_than_minimum_is_rejected(string? password)
    {
        Assert.Throws<DomainException>(() => User.EnsureValidPassword(password));
    }

    [Fact]
    public void Password_with_minimum_length_is_accepted()
    {
        User.EnsureValidPassword("12345678");
    }
}
