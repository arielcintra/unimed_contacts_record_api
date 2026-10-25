using System.Net.Http.Headers;
using ContactsRecord.Application.Auth;
using ContactsRecord.Domain.Common;
using ContactsRecord.Domain.Users;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace ContactsRecord.IntegrationTests;

/// <summary>Runs the real API against throwaway PostgreSQL and RabbitMQ containers (requires Docker).</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string SeedEmail = "admin@unimed.local";
    public const string SeedPassword = "Test@123";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder("rabbitmq:4-alpine").Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = _postgres.GetConnectionString(),
            ["RabbitMq:ConnectionString"] = _rabbitMq.GetConnectionString(),
            ["SeedUser:Email"] = SeedEmail,
            ["SeedUser:Password"] = SeedPassword,
            // Small batches so the tests also exercise multiple batches per file.
            ["Imports:BatchSize"] = "2",
        }));

    /// <summary>Client sending a valid access token, issued by the API's own token service.</summary>
    public HttpClient CreateAuthenticatedClient()
    {
        var user = User.Create(Email.Create(SeedEmail), "unused-hash", DateTimeOffset.UtcNow);
        var token = Services.GetRequiredService<ITokenService>().Generate(user);

        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        return client;
    }

    public Task InitializeAsync() => Task.WhenAll(_postgres.StartAsync(), _rabbitMq.StartAsync());

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
        await _rabbitMq.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
