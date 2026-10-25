using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ContactsRecord.Application.Auth;
using ContactsRecord.Application.Auth.Dtos;
using ContactsRecord.Application.Common.Dtos;
using ContactsRecord.Application.Users;
using ContactsRecord.Application.Users.Dtos;
using ContactsRecord.Domain.Common;
using ContactsRecord.Domain.Users;
using ContactsRecord.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace ContactsRecord.IntegrationTests.Users;

[Collection(ApiCollection.Name)]
public class UsersApiTests(ApiFactory factory)
{
    private const string Password = "Senha@123";

    private readonly HttpClient _admin = factory.CreateAuthenticatedClient();

    [Fact]
    public async Task Seed_user_logs_in_as_the_only_administrator()
    {
        var login = await LoginAsync(ApiFactory.SeedEmail, ApiFactory.SeedPassword);
        var users = await _admin.GetFromJsonAsync<PagedResultDto<UserOutputDto>>("/api/users?pageSize=100", ApiFactory.Json);

        Assert.Equal(UserRole.Admin, login.Role);
        var admin = Assert.Single(users!.Items, u => u.Role == UserRole.Admin);
        Assert.Equal(ApiFactory.SeedEmail, admin.Email);
        Assert.Equal(admin, users.Items[0]);
    }

    [Fact]
    public async Task Created_user_is_common_and_can_log_in()
    {
        var email = UniqueEmail();

        var response = await _admin.PostAsJsonAsync("/api/users", new UserInputDto(email, Password));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<UserOutputDto>(ApiFactory.Json);
        Assert.Equal(UserRole.Common, user!.Role);
        Assert.Equal(UserRole.Common, (await LoginAsync(email, Password)).Role);
    }

    [Fact]
    public async Task Create_with_email_in_use_returns_409()
    {
        var response = await _admin.PostAsJsonAsync("/api/users", new UserInputDto(ApiFactory.SeedEmail, Password));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(UserMessages.EmailInUse, problem!.Detail);
    }

    [Fact]
    public async Task Update_changes_email_and_password()
    {
        var user = await CreateAsync(UniqueEmail());
        var newEmail = UniqueEmail();

        var response = await _admin.PutAsJsonAsync($"/api/users/{user.Id}", new UserInputDto(newEmail, "NovaSenha@1"));

        response.EnsureSuccessStatusCode();
        Assert.Equal(UserRole.Common, (await LoginAsync(newEmail, "NovaSenha@1")).Role);
    }

    [Fact]
    public async Task Administrator_cannot_be_edited()
    {
        var users = await _admin.GetFromJsonAsync<PagedResultDto<UserOutputDto>>($"/api/users?search={ApiFactory.SeedEmail}", ApiFactory.Json);
        var admin = Assert.Single(users!.Items);

        var response = await _admin.PutAsJsonAsync($"/api/users/{admin.Id}", new UserInputDto("outro@unimed.local", Password));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Common_user_is_forbidden_to_list_create_or_edit_users()
    {
        var user = await CreateAsync(UniqueEmail());
        var common = factory.CreateClient();
        common.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", (await LoginAsync(user.Email, Password)).AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, (await common.GetAsync("/api/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await common.PostAsJsonAsync("/api/users", new UserInputDto(UniqueEmail(), Password))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await common.PutAsJsonAsync($"/api/users/{user.Id}", new UserInputDto(UniqueEmail(), Password))).StatusCode);

        // Contacts remain available to every authenticated user.
        Assert.Equal(HttpStatusCode.OK, (await common.GetAsync("/api/contacts")).StatusCode);
    }

    [Fact]
    public async Task Startup_keeps_the_configured_account_as_the_only_administrator()
    {
        var other = await CreateAsync(UniqueEmail());
        // Simulates a database where another account became admin (e.g. the configured e-mail changed).
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IUserRepository>().EnsureSingleAdminAsync(
                User.CreateAdmin(Email.Create(other.Email), "unused-hash", DateTimeOffset.UtcNow), CancellationToken.None);
        }

        await factory.Services.InitializeDatabaseAsync();

        Assert.Equal(UserRole.Admin, (await LoginAsync(ApiFactory.SeedEmail, ApiFactory.SeedPassword)).Role);
        Assert.Equal(UserRole.Common, (await LoginAsync(other.Email, Password)).Role);
    }

    [Fact]
    public async Task Users_endpoint_requires_authentication()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("/api/users")).StatusCode);
    }

    private async Task<UserOutputDto> CreateAsync(string email)
    {
        var response = await _admin.PostAsJsonAsync("/api/users", new UserInputDto(email, Password));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UserOutputDto>(ApiFactory.Json))!;
    }

    private async Task<LoginOutputDto> LoginAsync(string email, string password)
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginInputDto(email, password));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LoginOutputDto>(ApiFactory.Json))!;
    }

    private static string UniqueEmail() => $"user-{Guid.NewGuid():N}@unimed.local";
}
