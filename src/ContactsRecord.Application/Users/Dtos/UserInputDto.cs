namespace ContactsRecord.Application.Users.Dtos;

/// <summary>
/// Data to create or update a user. There is no role: users registered through the API are always common.
/// The password is required on creation; on update, a blank one keeps the current password.
/// </summary>
public sealed record UserInputDto(string? Email, string? Password);
