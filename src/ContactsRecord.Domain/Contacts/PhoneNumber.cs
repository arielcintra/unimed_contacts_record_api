using ContactsRecord.Domain.Common;

namespace ContactsRecord.Domain.Contacts;

/// <summary>
/// Brazilian phone number (landline or mobile) stored as digits only: DDD + 8 or 9 digits.
/// Unique across contacts.
/// </summary>
public sealed record PhoneNumber
{
    public const int MaxLength = 11;
    private const int MinLength = 10;
    private const string CountryCode = "55";

    public string Value { get; }

    private PhoneNumber(string value) => Value = value;

    public static PhoneNumber Create(string? raw)
    {
        var digits = ToDigits(raw);

        if (digits.Length is not (MinLength or MaxLength))
            throw new DomainException("Telefone inválido. Informe DDD + número, ex.: (11) 98765-4321.");

        return new PhoneNumber(digits);
    }

    public override string ToString() => Value;

    /// <summary>Strips formatting and an optional +55 prefix, e.g. "+55 (11) 98765-4321" → "11987654321".</summary>
    private static string ToDigits(string? raw)
    {
        var digits = new string((raw ?? string.Empty).Where(char.IsAsciiDigit).ToArray());

        var hasCountryCode = digits.Length is MinLength + 2 or MaxLength + 2
            && digits.StartsWith(CountryCode, StringComparison.Ordinal);

        return hasCountryCode ? digits[CountryCode.Length..] : digits;
    }
}
