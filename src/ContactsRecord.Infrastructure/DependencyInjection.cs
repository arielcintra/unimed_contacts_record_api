using ContactsRecord.Application.Auth;
using ContactsRecord.Application.Common;
using ContactsRecord.Application.Contacts;
using ContactsRecord.Application.Imports;
using ContactsRecord.Domain.Common;
using ContactsRecord.Domain.Users;
using ContactsRecord.Infrastructure.Auth;
using ContactsRecord.Infrastructure.Imports;
using ContactsRecord.Infrastructure.Messaging;
using ContactsRecord.Infrastructure.Persistence;
using Dapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

namespace ContactsRecord.Infrastructure;

public static class DependencyInjection
{
    private const string ConnectionStringName = "Default";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // Maps snake_case columns (is_active) to PascalCase properties (IsActive).
        DefaultTypeMap.MatchNamesWithUnderscores = true;

        services.AddSingleton(provider => NpgsqlDataSource.Create(GetConnectionString(provider)));
        services.AddDbContext<ContactsDbContext>((provider, options) => options
            .UseNpgsql(GetConnectionString(provider))
            .UseSnakeCaseNamingConvention());

        // One DbSession per scope: repositories share its connection and join its unit of work.
        services.AddScoped<DbSession>();
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<DbSession>());
        services.AddScoped<IContactRepository, ContactRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IContactImportRepository, ContactImportRepository>();

        return services.AddJwtAuthentication().AddContactImports();
    }

    private static IServiceCollection AddContactImports(this IServiceCollection services)
    {
        services.AddOptions<ImportOptions>().BindConfiguration(ImportOptions.SectionName)
            .ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<RabbitMqOptions>().BindConfiguration(RabbitMqOptions.SectionName)
            .ValidateDataAnnotations().ValidateOnStart();

        services.AddSingleton<ICsvContactReader, CsvContactReader>();
        services.AddSingleton<RabbitMqConnection>();
        services.AddSingleton<IImportQueue, RabbitMqImportQueue>();
        services.AddHostedService<ContactImportConsumer>();

        return services;
    }

    /// <summary>Applies pending migrations, then creates the configured user if it doesn't exist.</summary>
    public static async Task InitializeDatabaseAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;

        await provider.GetRequiredService<ContactsDbContext>().Database.MigrateAsync();

        var seed = provider.GetRequiredService<IOptions<SeedUserOptions>>().Value;
        var users = provider.GetRequiredService<IUserRepository>();
        var email = Email.Create(seed.Email);

        if (await users.GetByEmailAsync(email, CancellationToken.None) is not null) return;

        var passwordHash = provider.GetRequiredService<IPasswordHasher>().Hash(seed.Password);
        var now = provider.GetRequiredService<TimeProvider>().GetUtcNow();
        await users.AddAsync(User.Create(email, passwordHash, now), CancellationToken.None);
    }

    // Token generation and validation share the same options, so both are registered here.
    private static IServiceCollection AddJwtAuthentication(this IServiceCollection services)
    {
        services.AddOptions<JwtOptions>().BindConfiguration(JwtOptions.SectionName)
            .ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<SeedUserOptions>().BindConfiguration(SeedUserOptions.SectionName)
            .ValidateDataAnnotations().ValidateOnStart();

        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<IPasswordHasher, IdentityPasswordHasher>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = jwt.SigningKey(),
                    // The token expires exactly at the configured time.
                    ClockSkew = TimeSpan.Zero,
                };
            });

        return services;
    }

    // Resolved lazily so tests can override the connection string through configuration.
    private static string GetConnectionString(IServiceProvider provider) =>
        provider.GetRequiredService<IConfiguration>().GetConnectionString(ConnectionStringName)
        ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured.");
}
