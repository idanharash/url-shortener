using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using UrlShortener.Infrastructure.Observability;
using UrlShortener.Infrastructure.Persistence;
using UrlShortener.Model.Service;

namespace UrlShortener.Infrastructure.Messaging;

/// <summary>
/// Consumes click events and stores them. Delivery is at-least-once: a message is acked only after the
/// database update, so a crash in between can count a click twice. That is accepted for analytics
/// (see README, "Design decisions").
/// </summary>
public sealed class ClickQueueConsumer(
    IServiceScopeFactory scopeFactory,
    RabbitMqSettings settings,
    ILogger<ClickQueueConsumer> logger) : BackgroundService
{
    // Unacked messages the broker may push ahead; the async consumer still handles them one at a time.
    private const ushort PrefetchCount = 50;
    private const int ConnectRetrySeconds = 5;
    private static readonly TimeSpan ConnectRetryDelay = TimeSpan.FromSeconds(ConnectRetrySeconds);
    // While the database is down, requeued messages come straight back; this delay slows the loop down.
    private static readonly TimeSpan TransientFailureDelay = TimeSpan.FromSeconds(5);

    private readonly string _queue = settings.QueueName;
    private IConnection? _connection;
    private IModel? _channel;

    public bool IsConsuming => _channel is { IsOpen: true };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield(); // connecting blocks; don't hold up application startup

        // Retry the first connection instead of crashing the host. After that, the client's automatic
        // recovery reconnects and re-subscribes by itself.
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                StartConsuming();
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "RabbitMQ unavailable; retrying the consumer connection in {Delay}s", ConnectRetrySeconds);
                CloseConnection();
                await DelayUnlessStopping(ConnectRetryDelay, stoppingToken);
            }
        }

        await DelayUnlessStopping(Timeout.InfiniteTimeSpan, stoppingToken);
    }

    private void StartConsuming()
    {
        _connection = ClickQueueTopology.CreateConnectionFactory(settings, automaticRecovery: true)
            .CreateConnection("url-shortener-consumer");
        var channel = _channel = _connection.CreateModel();
        ClickQueueTopology.Declare(channel, _queue);
        channel.BasicQos(prefetchSize: 0, prefetchCount: PrefetchCount, global: false);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.Received += (_, delivery) => HandleDeliveryAsync(channel, delivery);
        channel.BasicConsume(queue: _queue, autoAck: false, consumer: consumer);

        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Consuming click events from {Queue}", _queue);
    }

    private async Task HandleDeliveryAsync(IModel channel, BasicDeliverEventArgs delivery)
    {
        using var activity = Telemetry.Messaging.StartActivity("click process", ActivityKind.Consumer);

        var code = TryReadCode(delivery);
        if (code is null)
        {
            logger.LogError("Malformed click message (delivery {DeliveryTag}); moving it to the dead-letter queue", delivery.DeliveryTag);
            DeadLetter(channel, delivery);
            return;
        }

        activity?.SetTag("url.code", code);
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IClickHandlerService>().HandleClickAsync(code);
        }
        catch (Exception ex) when (IsTransient(ex))
        {
            // Database unavailable: keep the message and slow down until it recovers. Nothing is lost.
            activity.MarkFailed(ex);
            logger.LogWarning(ex, "Database unavailable while storing click for {Code}; requeueing", code);
            await Task.Delay(TransientFailureDelay);
            channel.BasicNack(delivery.DeliveryTag, multiple: false, requeue: true);
            return;
        }
        catch (Exception ex)
        {
            // Not something a retry fixes (a bug or bad data): park it instead of blocking the queue.
            activity.MarkFailed(ex);
            logger.LogError(ex, "Storing click for {Code} failed; moving it to the dead-letter queue", code);
            DeadLetter(channel, delivery);
            return;
        }

        channel.BasicAck(delivery.DeliveryTag, multiple: false);
        MetricsRegistry.ClicksProcessed.Inc();
    }

    private static string? TryReadCode(BasicDeliverEventArgs delivery)
    {
        try
        {
            var message = JsonSerializer.Deserialize<ClickMessage>(delivery.Body.Span);
            return string.IsNullOrWhiteSpace(message?.Code) ? null : message.Code;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool IsTransient(Exception ex) => ex is BrokenCircuitException || TransientDbErrors.IsTransient(ex);

    private static void DeadLetter(IModel channel, BasicDeliverEventArgs delivery)
    {
        // requeue: false routes the message to the dead-letter queue configured in ClickQueueTopology.
        channel.BasicNack(delivery.DeliveryTag, multiple: false, requeue: false);
        MetricsRegistry.ClicksDeadLettered.Inc();
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        CloseConnection();
    }

    private void CloseConnection()
    {
        try { _channel?.Close(); } catch (Exception ex) { logger.LogDebug(ex, "Ignoring error while closing the consumer channel"); }
        try { _connection?.Close(); } catch (Exception ex) { logger.LogDebug(ex, "Ignoring error while closing the consumer connection"); }

        _channel?.Dispose();
        _connection?.Dispose();
        _channel = null;
        _connection = null;
    }

    private static async Task DelayUnlessStopping(TimeSpan delay, CancellationToken stoppingToken)
    {
        try { await Task.Delay(delay, stoppingToken); }
        catch (OperationCanceledException) { }
    }

    public override void Dispose()
    {
        CloseConnection();
        base.Dispose();
    }
}
