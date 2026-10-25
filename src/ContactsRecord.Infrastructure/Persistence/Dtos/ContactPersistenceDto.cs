using ContactsRecord.Domain.Contacts;
using EmailValue = ContactsRecord.Domain.Common.Email;

namespace ContactsRecord.Infrastructure.Persistence.Dtos;

/// <summary>
/// Row of the "contacts" table. Single source of the table shape: EF Core builds the schema
/// (migrations) from it and Dapper reads/writes it. Never leaves the Infrastructure layer.
/// </summary>
public sealed class ContactPersistenceDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = null!;
    public string? Email { get; init; }
    public string Phone { get; init; } = null!;
    public bool IsFavorite { get; init; }
    public bool IsActive { get; init; }

    // DateTime (UTC) instead of DateTimeOffset: it is what Npgsql returns for timestamptz.
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }

    public static ContactPersistenceDto FromDomain(Contact contact) => new()
    {
        Id = contact.Id,
        Name = contact.Name,
        Email = contact.Email?.Value,
        Phone = contact.Phone.Value,
        IsFavorite = contact.IsFavorite,
        IsActive = contact.IsActive,
        CreatedAt = contact.CreatedAt.UtcDateTime,
        UpdatedAt = contact.UpdatedAt.UtcDateTime,
    };

    public Contact ToDomain() => Contact.Restore(
        Id,
        Name,
        Email is null ? null : EmailValue.Create(Email),
        PhoneNumber.Create(Phone),
        IsFavorite,
        IsActive,
        AsUtc(CreatedAt),
        AsUtc(UpdatedAt));

    private static DateTimeOffset AsUtc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
