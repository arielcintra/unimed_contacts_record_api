namespace ContactsRecord.Application.Contacts.Dtos;

/// <summary>Data to create or update a contact. Validation happens in the domain.</summary>
public sealed record ContactInputDto(string? Name, string? Email, string? Phone);
