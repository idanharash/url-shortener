using System.ComponentModel.DataAnnotations;

namespace UrlShortener.Infrastructure.Messaging;

public sealed class RabbitMqSettings
{
    public const string SectionName = "RabbitMQ";

    [Required] public string Host { get; set; } = string.Empty;
    [Range(1, 65535)] public int Port { get; set; }
    [Required] public string Username { get; set; } = string.Empty;
    [Required] public string Password { get; set; } = string.Empty;
    [Required] public string QueueName { get; set; } = string.Empty;
}
