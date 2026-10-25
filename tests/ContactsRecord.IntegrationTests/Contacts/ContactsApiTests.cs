using System.Net;
using System.Net.Http.Json;
using ContactsRecord.Application.Common.Dtos;
using ContactsRecord.Application.Contacts;
using ContactsRecord.Application.Contacts.Dtos;
using Microsoft.AspNetCore.Mvc;
using static ContactsRecord.IntegrationTests.Contacts.ContactTestData;

namespace ContactsRecord.IntegrationTests.Contacts;

[Collection(ApiCollection.Name)]
public class ContactsApiTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateAuthenticatedClient();

    [Fact]
    public async Task Create_returns_201_with_normalized_contact()
    {
        var response = await _client.PostAsJsonAsync("/api/contacts", Input(phone: "+55 (21) 99876-0001", email: "Ana@Example.com"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var contact = await response.Content.ReadFromJsonAsync<ContactOutputDto>();
        Assert.Equal("21998760001", contact!.Phone);
        Assert.Equal("ana@example.com", contact.Email);
        Assert.True(contact.IsActive);
    }

    [Fact]
    public async Task Create_with_invalid_data_returns_400_problem_details()
    {
        var response = await _client.PostAsJsonAsync("/api/contacts", Input(phone: "123"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Contains("Telefone", problem!.Detail);
    }

    [Fact]
    public async Task Create_with_phone_of_inactive_contact_returns_409()
    {
        var contact = await CreateAsync(Input());
        await _client.PatchAsync($"/api/contacts/{contact.Id}/deactivate", null);

        var response = await _client.PostAsJsonAsync("/api/contacts", Input(phone: contact.Phone));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(ContactMessages.PhoneInUseByInactive, problem!.Detail);
    }

    [Fact]
    public async Task Update_changes_data_and_last_change_date()
    {
        var contact = await CreateAsync(Input());

        var response = await _client.PutAsJsonAsync($"/api/contacts/{contact.Id}", Input(name: "Novo Nome", phone: contact.Phone));

        response.EnsureSuccessStatusCode();
        var updated = await response.Content.ReadFromJsonAsync<ContactOutputDto>();
        Assert.Equal("Novo Nome", updated!.Name);
        Assert.True(updated.UpdatedAt > contact.UpdatedAt);
    }

    [Fact]
    public async Task Update_of_unknown_contact_returns_404()
    {
        var response = await _client.PutAsJsonAsync($"/api/contacts/{Guid.NewGuid()}", Input());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Search_finds_by_name_email_or_phone()
    {
        var token = UniqueToken();
        var byName = await CreateAsync(Input(name: $"Nome {token}"));
        var byEmail = await CreateAsync(Input(email: $"{token}@example.com"));
        var byPhone = await CreateAsync(Input());

        Assert.Equal([byName.Id], await SearchIdsAsync($"search=nome {token}"));
        Assert.Equal([byEmail.Id], await SearchIdsAsync($"search={token}@example"));
        Assert.Equal([byPhone.Id], await SearchIdsAsync($"search=({byPhone.Phone[..2]}) {byPhone.Phone[2..]}"));
    }

    [Fact]
    public async Task Search_filters_by_favorite_and_status_with_paging()
    {
        var token = UniqueToken();
        var favorite = await CreateAsync(Input(name: $"A {token}"));
        var inactive = await CreateAsync(Input(name: $"B {token}"));
        var plain = await CreateAsync(Input(name: $"C {token}"));
        await _client.PutAsync($"/api/contacts/{favorite.Id}/favorite", null);
        await _client.PatchAsync($"/api/contacts/{inactive.Id}/deactivate", null);

        Assert.Equal([favorite.Id], await SearchIdsAsync($"search={token}&isFavorite=true"));
        Assert.Equal([inactive.Id], await SearchIdsAsync($"search={token}&isActive=false"));

        var page = await _client.GetFromJsonAsync<PagedResultDto<ContactOutputDto>>(
            $"/api/contacts?search={token}&page=2&pageSize=2");
        Assert.Equal(3, page!.TotalCount);
        Assert.Equal(plain.Id, Assert.Single(page.Items).Id);
    }

    private async Task<ContactOutputDto> CreateAsync(ContactInputDto input)
    {
        var response = await _client.PostAsJsonAsync("/api/contacts", input);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ContactOutputDto>())!;
    }

    private async Task<Guid[]> SearchIdsAsync(string queryString)
    {
        var page = await _client.GetFromJsonAsync<PagedResultDto<ContactOutputDto>>($"/api/contacts?{queryString}");
        return page!.Items.Select(c => c.Id).ToArray();
    }
}
