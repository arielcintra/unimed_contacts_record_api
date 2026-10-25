using ContactsRecord.Application.Contacts.Dtos;

namespace ContactsRecord.IntegrationTests.Contacts;

/// <summary>Unique values so tests sharing one database never collide.</summary>
internal static class ContactTestData
{
    public static string UniquePhone() => $"11 9{Random.Shared.Next(0, 100_000_000):D8}";

    public static string UniqueToken() => Guid.NewGuid().ToString("N")[..10];

    public static ContactInputDto Input(string? name = null, string? phone = null, string? email = null) =>
        new(name ?? $"Contato {UniqueToken()}", email, phone ?? UniquePhone());
}
