using System.Globalization;
using FluentMigrator.Runner;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

// Same settings file as the web app (linked into the output folder),
// overridden by user-secrets and environment variables.
var configuration = new ConfigurationBuilder()
    .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: false)
    .AddUserSecrets(typeof(Program).Assembly, optional: true)
    .AddEnvironmentVariables()
    .Build();

var postgres = configuration.GetSection("Postgres");
string[] requiredKeys = ["Host", "Port", "Database", "Username", "Password"];
var missing = requiredKeys.Where(key => string.IsNullOrWhiteSpace(postgres[key])).ToList();
if (missing.Count > 0)
    throw new InvalidOperationException(
        $"Missing configuration: {string.Join(", ", missing.Select(key => $"Postgres:{key}"))}");

var connectionString = new NpgsqlConnectionStringBuilder
{
    Host = postgres["Host"],
    Port = int.Parse(postgres["Port"]!, CultureInfo.InvariantCulture),
    Database = postgres["Database"],
    Username = postgres["Username"],
    Password = postgres["Password"]
}.ConnectionString;

var serviceProvider = new ServiceCollection()
    .AddFluentMigratorCore()
    .ConfigureRunner(rb => rb
        .AddPostgres()
        .WithGlobalConnectionString(connectionString)
        .ScanIn(typeof(Program).Assembly).For.Migrations())
    .AddLogging(lb => lb.AddFluentMigratorConsole())
    .BuildServiceProvider();

using var scope = serviceProvider.CreateScope();
scope.ServiceProvider.GetRequiredService<IMigrationRunner>().MigrateUp();
