using ContactsRecord.Domain.Common;

namespace ContactsRecord.Domain.Users;

/// <summary>A user allowed to access the system. Only the password hash is kept, never the password.</summary>
public sealed class User : Entity
{
    public const int PasswordMinLength = 8;

    public Email Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public UserRole Role { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public bool IsAdmin => Role == UserRole.Admin;

    private User() { }

    /// <summary>Users registered through the system are always common users.</summary>
    public static User Create(Email email, string passwordHash, DateTimeOffset now) =>
        Create(email, passwordHash, UserRole.Common, now);

    /// <summary>The administrator, created only from the configuration.</summary>
    public static User CreateAdmin(Email email, string passwordHash, DateTimeOffset now) =>
        Create(email, passwordHash, UserRole.Admin, now);

    /// <summary>Rebuilds a user already persisted; does not apply creation rules.</summary>
    public static User Restore(Guid id, Email email, string passwordHash, UserRole role, DateTimeOffset createdAt) =>
        new() { Id = id, Email = email, PasswordHash = passwordHash, Role = role, CreatedAt = createdAt };

    /// <param name="passwordHash">New hash, or null to keep the current password.</param>
    public void Update(Email email, string? passwordHash)
    {
        // The administrator comes from the configuration: a change made here would not match it anymore.
        if (IsAdmin)
            throw new DomainException("O administrador é definido na configuração e não pode ser editado.");

        Email = email ?? throw new DomainException("E-mail é obrigatório.");
        if (passwordHash is not null) PasswordHash = RequireHash(passwordHash);
    }

    /// <summary>Rule for the plain password, checked before it is hashed.</summary>
    public static void EnsureValidPassword(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < PasswordMinLength)
            throw new DomainException($"Senha deve ter pelo menos {PasswordMinLength} caracteres.");
    }

    private static User Create(Email email, string passwordHash, UserRole role, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(now),
        Email = email ?? throw new DomainException("E-mail é obrigatório."),
        PasswordHash = RequireHash(passwordHash),
        Role = role,
        CreatedAt = now,
    };

    private static string RequireHash(string passwordHash) =>
        string.IsNullOrWhiteSpace(passwordHash)
            ? throw new DomainException("Hash de senha é obrigatório.")
            : passwordHash;
}
