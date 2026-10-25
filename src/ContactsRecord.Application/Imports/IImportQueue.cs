namespace ContactsRecord.Application.Imports;

/// <summary>Hands an import over to asynchronous processing.</summary>
public interface IImportQueue
{
    /// <exception cref="ImportQueueUnavailableException">The message could not be published.</exception>
    Task PublishAsync(Guid importId, CancellationToken cancellationToken);
}
