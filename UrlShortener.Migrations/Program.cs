using FluentMigrator.Runner;
using Microsoft.Extensions.DependencyInjection;
using System;

var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres");

if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.WriteLine("Missing environment variable: ConnectionStrings__Postgres");
    return;
}

var serviceProvider = new ServiceCollection()
    .AddFluentMigratorCore()
    .ConfigureRunner(rb => rb
        .AddPostgres()
        .WithGlobalConnectionString(connectionString)
        .ScanIn(typeof(Program).Assembly).For.Migrations())
    .AddLogging(lb => lb.AddFluentMigratorConsole())
    .BuildServiceProvider();

using var scope = serviceProvider.CreateScope();
var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();

runner.MigrateUp();
