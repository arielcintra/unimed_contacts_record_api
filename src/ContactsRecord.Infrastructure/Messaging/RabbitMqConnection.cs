using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace ContactsRecord.Infrastructure.Messaging;

/// <summary>Single long-lived connection shared by publisher and consumer, as RabbitMQ recommends.</summary>
internal sealed class RabbitMqConnection(IOptions<RabbitMqOptions> options) : IAsyncDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IConnection? _connection;

    public string QueueName => options.Value.QueueName;

    public async Task<IConnection> GetAsync(CancellationToken cancellationToken)
    {
        if (_connection is { IsOpen: true }) return _connection;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_connection is { IsOpen: true }) return _connection;
            if (_connection is not null) await _connection.DisposeAsync();

            var factory = new ConnectionFactory { Uri = new Uri(options.Value.ConnectionString) };
            _connection = await factory.CreateConnectionAsync(cancellationToken);
            return _connection;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Durable queue: survives a broker restart, together with the persistent messages in it.</summary>
    public Task DeclareQueueAsync(IChannel channel, CancellationToken cancellationToken) =>
        channel.QueueDeclareAsync(
            QueueName, durable: true, exclusive: false, autoDelete: false,
            cancellationToken: cancellationToken);

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null) await _connection.DisposeAsync();
        _lock.Dispose();
    }
}
