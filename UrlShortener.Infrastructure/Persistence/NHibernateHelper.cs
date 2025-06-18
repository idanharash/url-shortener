using FluentNHibernate.Cfg.Db;
using FluentNHibernate.Cfg;
using NHibernate;
using UrlShortener.Infrastructure.Persistence.Mappings;

public static class NHibernateHelper
{
    public static ISessionFactory CreateSessionFactory(string connectionString)
    {
        return Fluently.Configure()
            .Database(PostgreSQLConfiguration.Standard.ConnectionString(connectionString))
            .Mappings(m => m.FluentMappings.AddFromAssemblyOf<ShortUrlMap>())
            .BuildSessionFactory();
    }
}
