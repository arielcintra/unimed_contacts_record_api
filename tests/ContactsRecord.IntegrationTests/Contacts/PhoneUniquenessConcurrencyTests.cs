using System.Net;
using System.Net.Http.Json;
using ContactsRecord.Application.Common.Dtos;
using ContactsRecord.Application.Common.Exceptions;
using ContactsRecord.Application.Contacts;
using ContactsRecord.Application.Contacts.Dtos;
using ContactsRecord.Domain.Contacts;
using Microsoft.Extensions.DependencyInjection;
using static ContactsRecord.IntegrationTests.Contacts.ContactTestData;

namespace ContactsRecord.IntegrationTests.Contacts;

[Collection(ApiCollection.Name)]
public class PhoneUniquenessConcurrencyTests(ApiFactory factory)
{
    private const int ConcurrentRequests = 10;

    [Fact]
    public async Task Simultaneous_creations_with_same_phone_store_exactly_one_contact()
    {
        var client = factory.CreateAuthenticatedClient();
        var phone = UniquePhone();

        var responses = await Task.WhenAll(Enumerable.Range(0, ConcurrentRequests)
            .Select(_ => client.PostAsJsonAsync("/api/contacts", Input(phone: phone))));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.Created),
            r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));

        var stored = await client.GetFromJsonAsync<PagedResultDto<ContactOutputDto>>($"/api/contacts?search={phone}");
        Assert.Equal(1, stored!.TotalCount);
    }

    [Fact]
    public async Task Database_index_rejects_duplicates_that_bypass_the_service_pre_check()
    {
        var phone = PhoneNumber.Create(UniquePhone());
        var now = DateTimeOffset.UtcNow;

        var results = await Task.WhenAll(Enumerable.Range(0, ConcurrentRequests).Select(async i =>
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IContactRepository>();
            var contact = Contact.Create($"Concorrente {i}", null, phone, now);
            try
            {
                await repository.AddAsync(contact, CancellationToken.None);
                return true;
            }
            catch (ConflictException)
            {
                return false;
            }
        }));

        Assert.Single(results, stored => stored);
    }
}
