using RabbitMQ.Client;
using System.Text;
using System.Text.Json;
using UrlShortener.Application;
using UrlShortener.Model.QueueProducer;

namespace UrlShortener.BL
{
    public class ClickQueueProducer : IClickQueueProducer
    {
        private readonly RabbitMqSettings _settings;
        private readonly IConnection _connection;
        private readonly IModel _channel;

        public ClickQueueProducer(RabbitMqSettings settings)
        {
            _settings = settings;

            var factory = new ConnectionFactory
            {
                HostName = _settings.Host,
                Port = _settings.Port,
                UserName = _settings.Username,
                Password = _settings.Password,
                Ssl = new SslOption
                {
                    Enabled = false
                }
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
            _channel.QueueDeclare(queue: _settings.QueueName, durable: true, exclusive: false, autoDelete: false);
        }

        public void SendClick(string code)
        {
            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { Code = code }));
            _channel.BasicPublish(exchange: "", routingKey: _settings.QueueName, body: body);
        }
    }

}

