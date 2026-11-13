using RabbitMQ.Client;

namespace UrlShortener.Infrastructure.Messaging;

/// <summary>Click message contract shared by the publisher and the consumer.</summary>
public sealed record ClickMessage(string Code);

/// <summary>
/// Queue topology declared identically by the publisher and the consumer (RabbitMQ rejects a redeclaration
/// with different arguments). Messages that cannot be processed go to "{queue}.dead-letter" for inspection or replay.
/// </summary>
public static class ClickQueueTopology
{
    public static string DeadLetterQueue(string queue) => $"{queue}.dead-letter";

    public static void Declare(IModel channel, string queue)
    {
        var deadLetterQueue = DeadLetterQueue(queue);
        channel.QueueDeclare(deadLetterQueue, durable: true, exclusive: false, autoDelete: false, arguments: null);
        channel.QueueDeclare(queue, durable: true, exclusive: false, autoDelete: false, arguments: new Dictionary<string, object>
        {
            ["x-dead-letter-exchange"] = string.Empty,
            ["x-dead-letter-routing-key"] = deadLetterQueue
        });
    }

    public static ConnectionFactory CreateConnectionFactory(RabbitMqSettings settings, bool automaticRecovery) => new()
    {
        HostName = settings.Host,
        Port = settings.Port,
        UserName = settings.Username,
        Password = settings.Password,
        DispatchConsumersAsync = true,
        AutomaticRecoveryEnabled = automaticRecovery,
        TopologyRecoveryEnabled = automaticRecovery,
        NetworkRecoveryInterval = TimeSpan.FromSeconds(5),
        RequestedConnectionTimeout = TimeSpan.FromSeconds(5)
    };
}
