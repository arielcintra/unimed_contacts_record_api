using System.ComponentModel.DataAnnotations;

namespace ContactsRecord.Infrastructure.Messaging;

/// <summary>"RabbitMq" configuration section. Overridable by environment variables (e.g. RabbitMq__ConnectionString).</summary>
public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    /// <summary>AMQP URI, e.g. amqp://user:password@localhost:5672/.</summary>
    [Required]
    public string ConnectionString { get; init; } = null!;

    [Required]
    public string QueueName { get; init; } = "contact-imports";
}
