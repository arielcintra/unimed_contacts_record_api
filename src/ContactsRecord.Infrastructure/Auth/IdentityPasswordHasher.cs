using ContactsRecord.Domain.Users;
using Microsoft.AspNetCore.Identity;
using IPasswordHasher = ContactsRecord.Application.Auth.IPasswordHasher;

namespace ContactsRecord.Infrastructure.Auth;

/// <summary>Uses ASP.NET Core Identity's hasher (PBKDF2 with salt), shipped with the framework.</summary>
internal sealed class IdentityPasswordHasher : IPasswordHasher
{
    // The Identity hasher ignores the user instance, so a null user is safe here.
    private readonly PasswordHasher<User> _hasher = new();

    public string Hash(string password) => _hasher.HashPassword(null!, password);

    public bool Verify(string passwordHash, string password) =>
        _hasher.VerifyHashedPassword(null!, passwordHash, password) != PasswordVerificationResult.Failed;
}
