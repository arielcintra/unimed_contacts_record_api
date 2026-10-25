using ContactsRecord.Application.Common.Dtos;
using ContactsRecord.Application.Common.Exceptions;
using ContactsRecord.Application.Contacts.Dtos;
using ContactsRecord.Domain.Contacts;

namespace ContactsRecord.Application.Contacts;

public interface IContactRepository
{
    Task<Contact?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Looks up active and inactive contacts alike: the phone stays reserved after deactivation.</summary>
    Task<Contact?> GetByPhoneAsync(PhoneNumber phone, CancellationToken cancellationToken);

    Task<IReadOnlyList<Contact>> GetByPhonesAsync(IReadOnlyCollection<PhoneNumber> phones, CancellationToken cancellationToken);

    /// <summary>
    /// Inserts the contacts in a single statement, skipping those whose phone is already registered.
    /// </summary>
    /// <returns>Phones of the contacts actually inserted.</returns>
    Task<IReadOnlySet<string>> AddManyIgnoringDuplicatesAsync(
        IReadOnlyCollection<Contact> contacts, CancellationToken cancellationToken);

    /// <exception cref="ConflictException">The phone already belongs to another contact.</exception>
    Task AddAsync(Contact contact, CancellationToken cancellationToken);

    /// <exception cref="ConflictException">The phone already belongs to another contact.</exception>
    Task UpdateAsync(Contact contact, CancellationToken cancellationToken);

    /// <param name="query">Already normalized query.</param>
    Task<PagedResultDto<Contact>> SearchAsync(ContactQueryDto query, CancellationToken cancellationToken);
}
