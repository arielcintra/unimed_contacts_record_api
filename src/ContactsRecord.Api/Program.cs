using System.Text.Json.Serialization;
using ContactsRecord.Api.Errors;
using ContactsRecord.Application.Auth;
using ContactsRecord.Application.Contacts;
using ContactsRecord.Application.Imports;
using ContactsRecord.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;

const string BearerScheme = "Bearer";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    // Enums as text ("Completed", "Duplicate") instead of numbers.
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // "Authorize" button: paste the token returned by POST /api/auth/login.
    options.AddSecurityDefinition(BearerScheme, new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference(BearerScheme, document)] = [],
    });
});
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

// Every endpoint requires an authenticated user unless it opts out with [AllowAnonymous].
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ContactService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ContactImportService>();
builder.Services.AddScoped<ImportFileService>();
builder.Services.AddInfrastructure();

var app = builder.Build();

await app.Services.InitializeDatabaseAsync();

app.UseExceptionHandler();

// Interactive API docs at /swagger (Development is used by both `dotnet run` and docker compose).
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

await app.RunAsync();

// Exposes the entry point to WebApplicationFactory in the integration tests.
public partial class Program;
