using UrlShortener.Infrastructure.Messaging;
using UrlShortener.Model.QueueProducer;

namespace UrlShortener.Application.Extensions;

public static class MessagingExtensions
{
    public static IServiceCollection AddMessagingServices(this IServiceCollection services, IConfiguration config)
    {
        services.AddValidatedSettings<RabbitMqSettings>(config, RabbitMqSettings.SectionName);

        // One instance serves both roles: request threads enqueue clicks, the hosted service publishes them.
        services.AddSingleton<ClickQueueProducer>();
        services.AddSingleton<IClickQueueProducer>(sp => sp.GetRequiredService<ClickQueueProducer>());
        services.AddHostedService(sp => sp.GetRequiredService<ClickQueueProducer>());

        services.AddSingleton<ClickQueueConsumer>();
        services.AddHostedService(sp => sp.GetRequiredService<ClickQueueConsumer>());
        return services;
    }
}
