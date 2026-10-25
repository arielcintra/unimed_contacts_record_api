using System.ComponentModel.DataAnnotations;

namespace ContactsRecord.Infrastructure.Auth;

/// <summary>"SeedUser" configuration section: the user created at startup when it doesn't exist yet.</summary>
public sealed class SeedUserOptions
{
    public const string SectionName = "SeedUser";

    [Required]
    public string Email { get; init; } = null!;

    [Required]
    public string Password { get; init; } = null!;
}
