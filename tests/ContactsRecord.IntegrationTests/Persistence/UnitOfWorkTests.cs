using ContactsRecord.Application.Common;
using ContactsRecord.Application.Contacts;
using ContactsRecord.Domain.Contacts;
using Microsoft.Extensions.DependencyInjection;
using static ContactsRecord.IntegrationTests.Contacts.ContactTestData;

namespace ContactsRecord.IntegrationTests.Persistence;

[Collection(ApiCollection.Name)]
public class UnitOfWorkTests(ApiFactory factory)
{
    [Fact]
    public async Task Commits_every_change_when_the_work_succeeds()
    {
        var (first, second) = (NewContact(), NewContact());

        await RunInUnitOfWorkAsync(async contacts =>
        {
            await contacts.AddAsync(first, CancellationToken.None);
            await contacts.AddAsync(second, CancellationToken.None);
        });

        Assert.NotNull(await FindAsync(first));
        Assert.NotNull(await FindAsync(second));
    }

    [Fact]
    public async Task Rolls_back_every_change_when_the_work_fails()
    {
        var contact = NewContact();

        await Assert.ThrowsAsync<InvalidOperationException>(() => RunInUnitOfWorkAsync(async contacts =>
        {
            await contacts.AddAsync(contact, CancellationToken.None);
            throw new InvalidOperationException("Falha no meio do lote.");
        }));

        Assert.Null(await FindAsync(contact));
    }

    private static Contact NewContact() =>
        Contact.Create($"UoW {UniqueToken()}", null, PhoneNumber.Create(UniquePhone()), DateTimeOffset.UtcNow);

    private async Task RunInUnitOfWorkAsync(Func<IContactRepository, Task> work)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var contacts = scope.ServiceProvider.GetRequiredService<IContactRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        await unitOfWork.ExecuteAsync(() => work(contacts), CancellationToken.None);
    }

    // Separate scope: reads what was really committed.
    private async Task<Contact?> FindAsync(Contact contact)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IContactRepository>()
            .GetByIdAsync(contact.Id, CancellationToken.None);
    }
}
