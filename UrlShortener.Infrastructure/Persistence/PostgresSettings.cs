using System.ComponentModel.DataAnnotations;
using Npgsql;

namespace UrlShortener.Infrastructure.Persistence;

public sealed class PostgresSettings
{
    public const string SectionName = "Postgres";

    [Required] public string Host { get; set; } = string.Empty;
    [Range(1, 65535)] public int Port { get; set; }
    [Required] public string Database { get; set; } = string.Empty;
    [Required] public string Username { get; set; } = string.Empty;
    [Required] public string Password { get; set; } = string.Empty;

    public string ToConnectionString() => new NpgsqlConnectionStringBuilder
    {
        Host = Host,
        Port = Port,
        Database = Database,
        Username = Username,
        Password = Password
    }.ConnectionString;
}
