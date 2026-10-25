using System.Text.Json;
using ContactsRecord.Application.Imports;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace ContactsRecord.Infrastructure.Messaging;

/// <summary>
/// Listens to the import queue and hands each message to <see cref="ImportFileService"/>.
/// Hosted by the API process; holds no processing logic itself.
/// </summary>
internal sealed class ContactImportConsumer(
    RabbitMqConnection rabbit,
    IServiceScopeFactory scopeFactory,
    ILogger<ContactImportConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var connection = await rabbit.GetAsync(stoppingToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
        await rabbit.DeclareQueueAsync(channel, stoppingToken);

        // One import at a time per consumer; more API instances process more imports in parallel.
        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (_, delivery) => HandleAsync(channel, delivery, stoppingToken);
        await channel.BasicConsumeAsync(rabbit.QueueName, autoAck: false, consumer, stoppingToken);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
    }

    private async Task HandleAsync(IChannel channel, BasicDeliverEventArgs delivery, CancellationToken stoppingToken)
    {
        try
        {
            var message = JsonSerializer.Deserialize<ContactImportRequestedMessage>(delivery.Body.Span)!;

            await using var scope = scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ImportFileService>()
                .ProcessAsync(message.ImportId, stoppingToken);

            // Manual ack only after processing: if the API stops midway, the broker delivers the message again.
            await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Not acknowledged: the message returns to the queue and is processed after the restart.
        }
        catch (Exception ex)
        {
            // Retry once (e.g. database briefly unavailable); a second failure discards the message.
            logger.LogError(ex, "Failed to process contact import message (redelivered: {Redelivered}).", delivery.Redelivered);
            await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: !delivery.Redelivered);
        }
    }
}
