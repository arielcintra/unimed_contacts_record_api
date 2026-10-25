using ContactsRecord.Domain.Common;
using ContactsRecord.Domain.Contacts;

namespace ContactsRecord.Domain.Tests.Contacts;

public class PhoneNumberTests
{
    [Theory]
    [InlineData("11987654321", "11987654321")]
    [InlineData("(11) 98765-4321", "11987654321")]
    [InlineData("+55 (11) 98765-4321", "11987654321")]
    [InlineData("(11) 3456-7890", "1134567890")]
    [InlineData("+55 11 3456-7890", "1134567890")]
    public void Create_accepts_mobile_and_landline_normalized_to_digits(string raw, string expected)
    {
        Assert.Equal(expected, PhoneNumber.Create(raw).Value);
    }

    [Fact]
    public void Create_keeps_area_code_55_when_there_is_no_country_code()
    {
        Assert.Equal("55998765432", PhoneNumber.Create("(55) 99876-5432").Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("113456789")]    // 9 digits
    [InlineData("119876543210")] // 12 digits
    public void Create_rejects_invalid_numbers(string? raw)
    {
        Assert.Throws<DomainException>(() => PhoneNumber.Create(raw));
    }

    [Fact]
    public void Numbers_with_different_formatting_are_equal()
    {
        Assert.Equal(PhoneNumber.Create("(11) 98765-4321"), PhoneNumber.Create("+5511987654321"));
    }
}
