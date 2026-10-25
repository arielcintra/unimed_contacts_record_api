using ContactsRecord.Domain.Users;

namespace ContactsRecord.Application.Users.Dtos;

/// <summary>Never exposes the password hash.</summary>
public sealed record UserOutputDto(Guid Id, string Email, UserRole Role, DateTimeOffset CreatedAt)
{
    public static UserOutputDto From(User user) => new(user.Id, user.Email.Value, user.Role, user.CreatedAt);
}
