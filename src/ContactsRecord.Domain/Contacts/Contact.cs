using ContactsRecord.Domain.Common;

namespace ContactsRecord.Domain.Contacts;

public sealed class Contact : Entity
{
    public const int NameMaxLength = 150;

    public string Name { get; private set; } = null!;
    public Email? Email { get; private set; }

    /// <summary>Single phone field (landline or mobile), unique across contacts.</summary>
    public PhoneNumber Phone { get; private set; } = null!;
    public bool IsFavorite { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private Contact() { }

    public static Contact Create(string name, Email? email, PhoneNumber phone, DateTimeOffset now)
    {
        var contact = new Contact
        {
            Id = Guid.CreateVersion7(now),
            IsActive = true,
            CreatedAt = now,
        };
        contact.Update(name, email, phone, now);
        return contact;
    }

    /// <summary>Rebuilds a contact already persisted; does not apply creation rules.</summary>
    public static Contact Restore(
        Guid id,
        string name,
        Email? email,
        PhoneNumber phone,
        bool isFavorite,
        bool isActive,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt) =>
        new()
        {
            Id = id,
            Name = name,
            Email = email,
            Phone = phone,
            IsFavorite = isFavorite,
            IsActive = isActive,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
        };

    public void Update(string name, Email? email, PhoneNumber phone, DateTimeOffset now)
    {
        Name = NormalizeName(name);
        Email = email;
        Phone = phone ?? throw new DomainException("Telefone é obrigatório.");
        UpdatedAt = now;
    }

    // State transitions are idempotent: repeating a request leaves the contact unchanged.
    public void Activate(DateTimeOffset now) => SetActive(true, now);

    public void Deactivate(DateTimeOffset now) => SetActive(false, now);

    public void MarkAsFavorite(DateTimeOffset now) => SetFavorite(true, now);

    public void UnmarkAsFavorite(DateTimeOffset now) => SetFavorite(false, now);

    private void SetActive(bool active, DateTimeOffset now)
    {
        if (IsActive == active) return;
        IsActive = active;
        UpdatedAt = now;
    }

    private void SetFavorite(bool favorite, DateTimeOffset now)
    {
        if (IsFavorite == favorite) return;
        IsFavorite = favorite;
        UpdatedAt = now;
    }

    private static string NormalizeName(string? name)
    {
        var value = name?.Trim() ?? string.Empty;

        if (value.Length == 0)
            throw new DomainException("Nome é obrigatório.");
        if (value.Length > NameMaxLength)
            throw new DomainException($"Nome deve ter no máximo {NameMaxLength} caracteres.");

        return value;
    }
}
