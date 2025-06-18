using Microsoft.Extensions.Options;
using UrlShortener.Infrastructure.Messaging;
using UrlShortener.Model.QueueProducer;

namespace UrlShortener.Infrastructure.Extensions;

public static class MessagingExtensions
{
    public static IServiceCollection AddMessagingServices(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<RabbitMqSettings>(config.GetSection("RabbitMQ"));

        services.AddSingleton<IClickQueueProducer>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<RabbitMqSettings>>().Value;
            return new ClickQueueProducer(settings);
        });

        services.AddHostedService<ClickQueueConsumer>();

        return services;
    }
}
