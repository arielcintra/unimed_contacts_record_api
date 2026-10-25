namespace ContactsRecord.Infrastructure.Messaging;

/// <summary>
/// Published when a CSV file is uploaded. Carries only the id: the file itself stays in the
/// database, keeping messages small.
/// </summary>
internal sealed record ContactImportRequestedMessage(Guid ImportId);
