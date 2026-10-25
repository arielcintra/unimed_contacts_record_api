using ContactsRecord.Application.Contacts;
using ContactsRecord.Application.Contacts.Dtos;
using ContactsRecord.Domain.Contacts;
using NSubstitute;

namespace ContactsRecord.Application.Tests.Contacts;

/// <summary>Shared test doubles: a substitute repository and a frozen clock.</summary>
public abstract class ContactServiceTestsBase
{
    protected static readonly DateTimeOffset Now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);
    protected static readonly DateTimeOffset Yesterday = Now.AddDays(-1);

    protected IContactRepository Repository { get; } = Substitute.For<IContactRepository>();
    protected ContactService Service { get; }

    protected ContactServiceTestsBase()
    {
        var clock = Substitute.For<TimeProvider>();
        clock.GetUtcNow().Returns(Now);
        Service = new ContactService(Repository, clock);
    }

    protected static ContactInputDto Input(string phone = "(11) 98765-4321", string? email = "ana@example.com") =>
        new("Ana Silva", email, phone);

    /// <summary>A contact already in the repository, found by id and by phone.</summary>
    protected Contact GivenStoredContact(string phone = "11987654321", bool isActive = true)
    {
        var contact = Contact.Create("Existing", null, PhoneNumber.Create(phone), Yesterday);
        if (!isActive) contact.Deactivate(Yesterday);

        Repository.GetByIdAsync(contact.Id, Arg.Any<CancellationToken>()).Returns(contact);
        Repository.GetByPhoneAsync(contact.Phone, Arg.Any<CancellationToken>()).Returns(contact);
        return contact;
    }
}
