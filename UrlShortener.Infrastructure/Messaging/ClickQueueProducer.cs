using System.Diagnostics;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using UrlShortener.Infrastructure.Observability;
using UrlShortener.Model.QueueProducer;

namespace UrlShortener.Infrastructure.Messaging;

/// <summary>
/// Redirects hand clicks to a bounded in-memory buffer and return immediately; this background service
/// publishes them. It is the only user of its RabbitMQ channel (IModel is not thread-safe), and a broker
/// outage only fills the buffer instead of failing or slowing down redirects.
/// Trade-off: clicks still in the buffer are lost if the process crashes. Clicks are analytics, so that is accepted.
/// </summary>
public sealed class ClickQueueProducer(RabbitMqSettings settings, ILogger<ClickQueueProducer> logger)
    : BackgroundService, IClickQueueProducer
{
    // Bounds memory during a long outage; beyond it, clicks are dropped and counted in url_clicks_dropped_total.
    private const int BufferCapacity = 10_000;
    private const int ReconnectSeconds = 5;
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(ReconnectSeconds);
    private static readonly TimeSpan ShutdownFlushTimeout = TimeSpan.FromSeconds(5);

    private readonly Channel<string> _buffer = Channel.CreateBounded<string>(
        new BoundedChannelOptions(BufferCapacity) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });

    private readonly string _queue = settings.QueueName;
    private IConnection? _connection;
    private IModel? _channel;

    public bool IsConnected => _channel is { IsOpen: true };

    public bool EnqueueClick(string code)
    {
        if (_buffer.Writer.TryWrite(code))
            return true;

        MetricsRegistry.ClicksDropped.Inc();
        return false;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield(); // connecting blocks; don't hold up application startup

        while (!stoppingToken.IsCancellationRequested)
        {
            if (!TryConnect())
            {
                await DelayUnlessStopping(ReconnectDelay, stoppingToken);
                continue;
            }

            try
            {
                while (await _buffer.Reader.WaitToReadAsync(stoppingToken))
                {
                    // Peek, publish, then remove: a click is only taken out of the buffer once it was handed to the broker.
                    while (_buffer.Reader.TryPeek(out var code))
                    {
                        Publish(code);
                        _buffer.Reader.TryRead(out _);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Publishing click events failed; reconnecting");
                CloseConnection();
            }
        }

        FlushOnShutdown();
    }

    private bool TryConnect()
    {
        if (_channel is { IsOpen: true })
            return true;

        try
        {
            CloseConnection();
            _connection = ClickQueueTopology.CreateConnectionFactory(settings, automaticRecovery: false)
                .CreateConnection("url-shortener-publisher");
            _channel = _connection.CreateModel();
            ClickQueueTopology.Declare(_channel, _queue);
            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation("Connected to RabbitMQ; publishing click events to {Queue}", _queue);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "RabbitMQ unavailable; buffering clicks (up to {Capacity}) and retrying in {Delay}s",
                BufferCapacity, ReconnectSeconds);
            CloseConnection();
            return false;
        }
    }

    private void Publish(string code)
    {
        using var activity = Telemetry.Messaging.StartActivity("click publish", ActivityKind.Producer);
        var properties = _channel!.CreateBasicProperties();
        properties.Persistent = true; // survives a broker restart, matching the durable queue
        properties.ContentType = "application/json";

        _channel.BasicPublish(
            exchange: string.Empty,
            routingKey: _queue,
            basicProperties: properties,
            body: JsonSerializer.SerializeToUtf8Bytes(new ClickMessage(code)));

        MetricsRegistry.ClicksPublished.Inc();
    }

    private void FlushOnShutdown()
    {
        var deadline = DateTime.UtcNow + ShutdownFlushTimeout;
        try
        {
            while (DateTime.UtcNow < deadline && _buffer.Reader.TryPeek(out var code) && TryConnect())
            {
                Publish(code);
                _buffer.Reader.TryRead(out _);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not flush buffered clicks on shutdown");
        }

        var lost = _buffer.Reader.Count;
        if (lost > 0)
        {
            MetricsRegistry.ClicksDropped.Inc(lost);
            logger.LogWarning("{Count} buffered clicks were not published before shutdown", lost);
        }

        CloseConnection();
    }

    private void CloseConnection()
    {
        try { _channel?.Close(); } catch (Exception ex) { logger.LogDebug(ex, "Ignoring error while closing the publisher channel"); }
        try { _connection?.Close(); } catch (Exception ex) { logger.LogDebug(ex, "Ignoring error while closing the publisher connection"); }

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
