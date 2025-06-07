namespace UrlShortener.Application
{
    public class RabbitMqSettings
    {
        public string Host { get; set; } = null!;
        public int Port { get; set; }
        public string Username { get; set; } = null!;
        public string Password { get; set; } = null!;
        public string QueueName { get; set; } = null!;
    }
}
