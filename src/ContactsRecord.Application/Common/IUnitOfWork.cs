namespace ContactsRecord.Application.Common;

/// <summary>
/// Groups repository calls into a single database transaction: either every change is saved or none is.
/// Repositories used inside <paramref name="work"/> join the transaction automatically.
/// </summary>
public interface IUnitOfWork
{
    Task ExecuteAsync(Func<Task> work, CancellationToken cancellationToken);
}
