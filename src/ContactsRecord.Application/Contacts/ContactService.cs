using ContactsRecord.Application.Common.Dtos;
using ContactsRecord.Application.Common.Exceptions;
using ContactsRecord.Application.Contacts.Dtos;
using ContactsRecord.Domain.Contacts;

namespace ContactsRecord.Application.Contacts;

public sealed class ContactService(IContactRepository contacts, TimeProvider clock)
{
    public async Task<PagedResultDto<ContactOutputDto>> SearchAsync(
        ContactQueryDto query, CancellationToken cancellationToken)
    {
        var result = await contacts.SearchAsync(query.Normalize(), cancellationToken);
        return result.Map(ContactOutputDto.From);
    }

    public async Task<ContactOutputDto> CreateAsync(ContactInputDto input, CancellationToken cancellationToken)
    {
        var contact = input.ToNewContact(clock.GetUtcNow());

        await EnsurePhoneIsAvailableAsync(contact, cancellationToken);
        await contacts.AddAsync(contact, cancellationToken);

        return ContactOutputDto.From(contact);
    }

    public async Task<ContactOutputDto> UpdateAsync(
        Guid id, ContactInputDto input, CancellationToken cancellationToken)
    {
        var contact = await GetExistingAsync(id, cancellationToken);
        var (email, phone) = input.ToValueObjects();
        contact.Update(input.Name ?? string.Empty, email, phone, clock.GetUtcNow());

        await EnsurePhoneIsAvailableAsync(contact, cancellationToken);
        await contacts.UpdateAsync(contact, cancellationToken);

        return ContactOutputDto.From(contact);
    }

    public Task<ContactOutputDto> ActivateAsync(Guid id, CancellationToken cancellationToken) =>
        ChangeAsync(id, (contact, now) => contact.Activate(now), cancellationToken);

    public Task<ContactOutputDto> DeactivateAsync(Guid id, CancellationToken cancellationToken) =>
        ChangeAsync(id, (contact, now) => contact.Deactivate(now), cancellationToken);

    public Task<ContactOutputDto> MarkAsFavoriteAsync(Guid id, CancellationToken cancellationToken) =>
        ChangeAsync(id, (contact, now) => contact.MarkAsFavorite(now), cancellationToken);

    public Task<ContactOutputDto> UnmarkAsFavoriteAsync(Guid id, CancellationToken cancellationToken) =>
        ChangeAsync(id, (contact, now) => contact.UnmarkAsFavorite(now), cancellationToken);

    private async Task<ContactOutputDto> ChangeAsync(
        Guid id, Action<Contact, DateTimeOffset> change, CancellationToken cancellationToken)
    {
        var contact = await GetExistingAsync(id, cancellationToken);
        change(contact, clock.GetUtcNow());
        await contacts.UpdateAsync(contact, cancellationToken);
        return ContactOutputDto.From(contact);
    }

    private async Task<Contact> GetExistingAsync(Guid id, CancellationToken cancellationToken) =>
        await contacts.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException(ContactMessages.NotFound);

    // Friendly pre-check. Concurrent requests can still pass it together; the unique
    // index in the database is the real guarantee and the repository reports it as a conflict.
    private async Task EnsurePhoneIsAvailableAsync(Contact contact, CancellationToken cancellationToken)
    {
        var owner = await contacts.GetByPhoneAsync(contact.Phone, cancellationToken);
        if (owner is null || owner.Id == contact.Id) return;

        throw new ConflictException(owner.IsActive
            ? ContactMessages.PhoneInUse
            : ContactMessages.PhoneInUseByInactive);
    }
}
