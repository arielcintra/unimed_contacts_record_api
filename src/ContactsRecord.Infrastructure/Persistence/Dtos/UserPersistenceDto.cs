using ContactsRecord.Domain.Users;
using EmailValue = ContactsRecord.Domain.Common.Email;

namespace ContactsRecord.Infrastructure.Persistence.Dtos;

/// <summary>
/// Row of the "users" table: EF Core builds the schema from it and Dapper reads/writes it.
/// Never leaves the Infrastructure layer.
/// </summary>
public sealed class UserPersistenceDto
{
    public Guid Id { get; init; }
    public string Email { get; init; } = null!;
    public string PasswordHash { get; init; } = null!;

    // DateTime (UTC) instead of DateTimeOffset: it is what Npgsql returns for timestamptz.
    public DateTime CreatedAt { get; init; }

    public static UserPersistenceDto FromDomain(User user) => new()
    {
        Id = user.Id,
        Email = user.Email.Value,
        PasswordHash = user.PasswordHash,
        CreatedAt = user.CreatedAt.UtcDateTime,
    };

    public User ToDomain() => User.Restore(
        Id,
        EmailValue.Create(Email),
        PasswordHash,
        new DateTimeOffset(DateTime.SpecifyKind(CreatedAt, DateTimeKind.Utc)));
}
