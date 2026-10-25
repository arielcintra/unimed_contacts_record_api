using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace ContactsRecord.Infrastructure.Auth;

/// <summary>"Jwt" configuration section. Every value can be overridden by environment variables (e.g. Jwt__ExpirationMinutes).</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; init; } = null!;

    [Required]
    public string Audience { get; init; } = null!;

    /// <summary>HMAC-SHA256 signing key; at least 32 characters (256 bits).</summary>
    [Required, MinLength(32)]
    public string Key { get; init; } = null!;

    [Range(1, 24 * 60)]
    public int ExpirationMinutes { get; init; }

    public SymmetricSecurityKey SigningKey() => new(Encoding.UTF8.GetBytes(Key));
}
