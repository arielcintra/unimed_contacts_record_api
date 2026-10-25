namespace ContactsRecord.Application.Imports;

/// <summary>Thrown by <see cref="IImportQueue"/> when the message can't be published.</summary>
public sealed class ImportQueueUnavailableException(Exception innerException)
    : Exception("Message broker unavailable.", innerException);
