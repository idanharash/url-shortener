using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using UrlShortener.Model.Repository;
using UrlShortener.Model.Service;

namespace UrlShortener.Application;

public class ClickQueueConsumer : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RabbitMqSettings _settings;
    private IConnection? _connection;
    private IModel? _channel;
    private readonly Policy _resiliencePolicy;

    public ClickQueueConsumer(IServiceScopeFactory scopeFactory, IOptions<RabbitMqSettings> options)
    {
        _scopeFactory = scopeFactory;
        _settings = options.Value;

        // הגדרת פוליסת Polly עם Retry + Circuit Breaker
        _resiliencePolicy = Policy
            .Handle<Exception>()
            .Retry(3, (ex, retryCount) =>
            {
                Console.WriteLine($"[RabbitMQ Retry] Attempt {retryCount}: {ex.Message}");
            })
            .Wrap(Policy
                .Handle<Exception>()
                .CircuitBreaker(2, TimeSpan.FromSeconds(15),
                    onBreak: (ex, duration) =>
                    {
                        Console.WriteLine($"[Circuit Broken] {ex.Message} for {duration.TotalSeconds} seconds");
                    },
                    onReset: () => Console.WriteLine("[Circuit Reset] RabbitMQ connection restored")
                )
            );
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.Run(() =>
        {
            _resiliencePolicy.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(_settings.QueueName))
                    throw new InvalidOperationException("RabbitMQ.QueueName is not set");

                var factory = new ConnectionFactory
                {
                    HostName = _settings.Host,
                    Port = _settings.Port,
                    UserName = _settings.Username,
                    Password = _settings.Password
                };

                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();

                _channel.QueueDeclare(
                    queue: _settings.QueueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null
                );

                var consumer = new EventingBasicConsumer(_channel);
                consumer.Received += async (sender, ea) =>
                {
                    var body = ea.Body.ToArray();
                    var json = Encoding.UTF8.GetString(body);

                    try
                    {
                        var data = JsonSerializer.Deserialize<ClickEvent>(json);
                        if (!string.IsNullOrEmpty(data?.Code))
                        {
                            using var scope = _scopeFactory.CreateScope();
                            var service = scope.ServiceProvider.GetRequiredService<IClickHandlerService>();
                            await service.HandleClickAsync(data.Code);
                        }

                        _channel.BasicAck(ea.DeliveryTag, multiple: false);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Message Error] {ex.Message}");
                        // שים לב: אפשר לבצע Nack או Dead-letter כאן במידת הצורך
                    }
                };

                _channel.BasicConsume(
                    queue: _settings.QueueName,
                    autoAck: false,
                    consumer: consumer
                );

                Console.WriteLine("[RabbitMQ Consumer] Listening to queue...");
            });
        }, stoppingToken);
    }

    public override void Dispose()
    {
        _channel?.Close();
        _connection?.Close();
        base.Dispose();
    }

    private class ClickEvent
    {
        public string? Code { get; set; }
    }
}
