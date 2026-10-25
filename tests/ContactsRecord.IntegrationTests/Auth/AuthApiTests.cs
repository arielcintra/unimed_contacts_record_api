using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ContactsRecord.Application.Auth;
using ContactsRecord.Application.Auth.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace ContactsRecord.IntegrationTests.Auth;

[Collection(ApiCollection.Name)]
public class AuthApiTests(ApiFactory factory)
{
    private const string ProtectedEndpoint = "/api/contacts";

    [Fact]
    public async Task Login_with_seed_user_returns_token_that_grants_access()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginInputDto(ApiFactory.SeedEmail, ApiFactory.SeedPassword));

        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<LoginOutputDto>(ApiFactory.Json);
        Assert.False(string.IsNullOrWhiteSpace(login!.AccessToken));
        Assert.InRange(login.ExpiresAt, DateTimeOffset.UtcNow.AddMinutes(59), DateTimeOffset.UtcNow.AddMinutes(61));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(ProtectedEndpoint)).StatusCode);
    }

    [Fact]
    public async Task Login_with_wrong_password_returns_401()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new LoginInputDto(ApiFactory.SeedEmail, "wrong-password"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(AuthMessages.InvalidCredentials, problem!.Detail);
    }

    [Fact]
    public async Task Request_without_token_returns_401()
    {
        var response = await factory.CreateClient().GetAsync(ProtectedEndpoint);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Request_with_invalid_token_returns_401()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-valid-token");

        var response = await client.GetAsync(ProtectedEndpoint);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
