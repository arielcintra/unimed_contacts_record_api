using System.Text.RegularExpressions;

namespace ContactsRecord.Domain.Common;

/// <summary>E-mail address normalized to trimmed lower case.</summary>
public sealed partial record Email
{
    public const int MaxLength = 254;

    public string Value { get; }

    private Email(string value) => Value = value;

    public static Email Create(string? raw)
    {
        var value = (raw ?? string.Empty).Trim().ToLowerInvariant();

        if (value.Length > MaxLength || !Pattern().IsMatch(value))
            throw new DomainException("E-mail inválido.");

        return new Email(value);
    }

    public override string ToString() => Value;

    // Intentionally permissive: one "@", no spaces, a dot in the domain.
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex Pattern();
}
