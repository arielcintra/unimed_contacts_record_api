using ContactsRecord.Domain.Common;

namespace ContactsRecord.Domain.Users;

/// <summary>A user allowed to access the system. Only the password hash is kept, never the password.</summary>
public sealed class User : Entity
{
    public Email Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }

    private User() { }

    public static User Create(Email email, string passwordHash, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new DomainException("Hash de senha é obrigatório.");

        return new User
        {
            Id = Guid.CreateVersion7(now),
            Email = email ?? throw new DomainException("E-mail é obrigatório."),
            PasswordHash = passwordHash,
            CreatedAt = now,
        };
    }

    /// <summary>Rebuilds a user already persisted; does not apply creation rules.</summary>
    public static User Restore(Guid id, Email email, string passwordHash, DateTimeOffset createdAt) =>
        new() { Id = id, Email = email, PasswordHash = passwordHash, CreatedAt = createdAt };
}
