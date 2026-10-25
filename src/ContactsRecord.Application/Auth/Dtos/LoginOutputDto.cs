namespace ContactsRecord.Application.Auth.Dtos;

public sealed record LoginOutputDto(string AccessToken, DateTimeOffset ExpiresAt);
