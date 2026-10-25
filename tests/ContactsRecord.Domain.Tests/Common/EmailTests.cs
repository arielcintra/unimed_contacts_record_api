using ContactsRecord.Domain.Common;

namespace ContactsRecord.Domain.Tests.Common;

public class EmailTests
{
    [Fact]
    public void Create_trims_and_lowercases()
    {
        Assert.Equal("ana.silva@example.com", Email.Create("  Ana.Silva@Example.COM ").Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ana")]
    [InlineData("ana@example")]
    [InlineData("ana silva@example.com")]
    [InlineData("ana@@example.com")]
    public void Create_rejects_invalid_addresses(string raw)
    {
        Assert.Throws<DomainException>(() => Email.Create(raw));
    }

    [Fact]
    public void Create_rejects_addresses_longer_than_max_length()
    {
        var raw = new string('a', Email.MaxLength) + "@example.com";

        Assert.Throws<DomainException>(() => Email.Create(raw));
    }
}
