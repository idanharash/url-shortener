using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using UrlShortener.Infrastructure.DependencyInjection;
using UrlShortener.Model.Repository;

namespace UrlShortener.UnitTests;

public class PostgresRegistrationTests
{
    [Fact]
    public void AddPostgres_DoesNotConnectToTheDatabase_SoTheAppCanStartWhilePostgresIsDown()
    {
        // Nothing listens on port 1. Registering and resolving the persistence services must not open a
        // connection; the first query is where an unreachable database surfaces, as a transient error.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Postgres:Host"] = "localhost",
                ["Postgres:Port"] = "1",
                ["Postgres:Database"] = "url_shortener",
                ["Postgres:Username"] = "postgres",
                ["Postgres:Password"] = "not-used"
            })
            .Build();

        using var provider = new ServiceCollection()
            .AddLogging()
            .AddPostgres(configuration)
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<NpgsqlDataSource>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IUrlRepository>());
    }
}
