using ContactsRecord.Application.Common;
using Dapper;
using Npgsql;

namespace ContactsRecord.Infrastructure.Persistence;

/// <summary>
/// One connection per scope (HTTP request or consumed message), shared by every repository, plus the
/// transaction of the current unit of work, if any. Commands outside a unit of work run on their own.
/// </summary>
internal sealed class DbSession(NpgsqlDataSource dataSource) : IUnitOfWork, IAsyncDisposable
{
    private NpgsqlConnection? _connection;
    private NpgsqlTransaction? _transaction;

    public async Task ExecuteAsync(Func<Task> work, CancellationToken cancellationToken)
    {
        if (_transaction is not null)
            throw new InvalidOperationException("Nested units of work are not supported.");

        var connection = await GetConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        _transaction = transaction;
        try
        {
            await work();
            await transaction.CommitAsync(cancellationToken);
        }
        finally
        {
            // Not committed (exception) → rolled back when disposed.
            _transaction = null;
        }
    }

    // Dapper calls that automatically join the current transaction.

    public async Task<IEnumerable<T>> QueryAsync<T>(string sql, object? parameters, CancellationToken cancellationToken) =>
        await (await GetConnectionAsync(cancellationToken)).QueryAsync<T>(Command(sql, parameters, cancellationToken));

    public async Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object? parameters, CancellationToken cancellationToken) =>
        await (await GetConnectionAsync(cancellationToken)).QuerySingleOrDefaultAsync<T>(Command(sql, parameters, cancellationToken));

    public async Task<T> QuerySingleAsync<T>(string sql, object? parameters, CancellationToken cancellationToken) =>
        await (await GetConnectionAsync(cancellationToken)).QuerySingleAsync<T>(Command(sql, parameters, cancellationToken));

    public async Task<SqlMapper.GridReader> QueryMultipleAsync(string sql, object? parameters, CancellationToken cancellationToken) =>
        await (await GetConnectionAsync(cancellationToken)).QueryMultipleAsync(Command(sql, parameters, cancellationToken));

    public async Task ExecuteCommandAsync(string sql, object? parameters, CancellationToken cancellationToken) =>
        await (await GetConnectionAsync(cancellationToken)).ExecuteAsync(Command(sql, parameters, cancellationToken));

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null) await _connection.DisposeAsync();
    }

    private async Task<NpgsqlConnection> GetConnectionAsync(CancellationToken cancellationToken) =>
        _connection ??= await dataSource.OpenConnectionAsync(cancellationToken);

    private CommandDefinition Command(string sql, object? parameters, CancellationToken cancellationToken) =>
        new(sql, parameters, _transaction, cancellationToken: cancellationToken);
}
