using FluentNHibernate.Cfg;
using FluentNHibernate.Cfg.Db;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using UrlShortener.Application.Settings;

namespace UrlShortener.Infrastructure.Extensions;

public static class PersistenceExtensions
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration config)
    {
        // Redis Settings
        services.Configure<RedisSettings>(config.GetSection("Redis"));

        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<RedisSettings>>().Value;
            return ConnectionMultiplexer.Connect(new ConfigurationOptions
            {
                EndPoints = { settings.ConnectionString },
                ConnectTimeout = 2000,
                AbortOnConnectFail = false
            });
        });

        // PostgreSQL via NHibernate
        var connStr = config.GetConnectionString("Postgres")
                     ?? throw new InvalidOperationException("Missing PostgreSQL connection string");

        var sessionFactory = Fluently.Configure()
            .Database(PostgreSQLConfiguration.Standard.ConnectionString(connStr))
            .Mappings(m => m.FluentMappings.AddFromAssemblyOf<UrlRepository>())
            .BuildSessionFactory();

        services.AddSingleton(sessionFactory);
        return services;
    }
}
