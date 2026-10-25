using ContactsRecord.Application.Contacts.Dtos;
using ContactsRecord.Domain.Common;
using ContactsRecord.Domain.Contacts;

namespace ContactsRecord.Application.Contacts;

/// <summary>Single place that turns raw contact input into domain objects (manual form and CSV import).</summary>
internal static class ContactInputMapping
{
    public static Contact ToNewContact(this ContactInputDto input, DateTimeOffset now)
    {
        var (email, phone) = input.ToValueObjects();
        return Contact.Create(input.Name ?? string.Empty, email, phone, now);
    }

    public static (Email? Email, PhoneNumber Phone) ToValueObjects(this ContactInputDto input) => (
        string.IsNullOrWhiteSpace(input.Email) ? null : Email.Create(input.Email),
        PhoneNumber.Create(input.Phone));
}
