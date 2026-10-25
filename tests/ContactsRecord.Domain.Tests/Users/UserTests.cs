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
}
