using FluentNHibernate.Cfg;
using FluentNHibernate.Cfg.Db;
using StackExchange.Redis;
using UrlShortener.Application.Settings;
using UrlShortener.Infrastructure.Caching;
using UrlShortener.Infrastructure.Persistence;

namespace UrlShortener.Application.Extensions;

public static class PersistenceExtensions
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration config)
    {
        var redis = services.AddValidatedSettings<RedisSettings>(config, RedisSettings.SectionName);
        services.AddValidatedSettings<CacheSettings>(config, CacheSettings.SectionName);
        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var options = ConfigurationOptions.Parse(redis.ConnectionString);
            options.AbortOnConnectFail = false; // start even if Redis is down; it reconnects in the background
            options.ConnectTimeout = 2000;
            options.AsyncTimeout = 1000;
            // While disconnected, fail commands immediately instead of queueing them until the timeout:
            // the cache falls back to the database, so waiting would only slow down redirects.
            options.BacklogPolicy = BacklogPolicy.FailFast;
            return ConnectionMultiplexer.Connect(options);
        });

        var postgres = services.AddValidatedSettings<PostgresSettings>(config, PostgresSettings.SectionName);
        var sessionFactory = Fluently.Configure()
            .Database(PostgreSQLConfiguration.Standard.ConnectionString(postgres.ToConnectionString()))
            .Mappings(m => m.FluentMappings.AddFromAssemblyOf<UrlRepository>())
            .BuildSessionFactory();

        services.AddSingleton(sessionFactory);
        return services;
    }
}
