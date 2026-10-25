using ContactsRecord.Domain.Users;

namespace ContactsRecord.Application.Auth.Dtos;

/// <param name="Role">Lets the frontend show only what the user can access; the API enforces it on its own.</param>
public sealed record LoginOutputDto(string AccessToken, DateTimeOffset ExpiresAt, UserRole Role);
