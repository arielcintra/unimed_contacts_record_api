using ContactsRecord.Domain.Contacts;

namespace ContactsRecord.Application.Contacts.Dtos;

public sealed record ContactOutputDto(
    Guid Id,
    string Name,
    string? Email,
    string Phone,
    bool IsFavorite,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static ContactOutputDto From(Contact contact) => new(
        contact.Id,
        contact.Name,
        contact.Email?.Value,
        contact.Phone.Value,
        contact.IsFavorite,
        contact.IsActive,
        contact.CreatedAt,
        contact.UpdatedAt);
}
