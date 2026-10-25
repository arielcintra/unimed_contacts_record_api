using System.Text.Json;
using ContactsRecord.Application.Imports;
using RabbitMQ.Client;

namespace ContactsRecord.Infrastructure.Messaging;

internal sealed class RabbitMqImportQueue(RabbitMqConnection rabbit) : IImportQueue
{
    // Publisher confirms: PublishAsync only completes after the broker has stored the message.
    private static readonly CreateChannelOptions ConfirmedChannel = new(
        publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true);

    public async Task PublishAsync(Guid importId, CancellationToken cancellationToken)
    {
        try
        {
            var connection = await rabbit.GetAsync(cancellationToken);
            await using var channel = await connection.CreateChannelAsync(ConfirmedChannel, cancellationToken);
            await rabbit.DeclareQueueAsync(channel, cancellationToken);

            var body = JsonSerializer.SerializeToUtf8Bytes(new ContactImportRequestedMessage(importId));
            var properties = new BasicProperties { Persistent = true, ContentType = "application/json" };

            await channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: rabbit.QueueName,
                mandatory: false,
                basicProperties: properties,
                body: body,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new ImportQueueUnavailableException(ex);
        }
    }
}
