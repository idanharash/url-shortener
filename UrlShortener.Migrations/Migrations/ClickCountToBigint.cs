using FluentMigrator;

namespace UrlShortener.Migrations.Migrations;

/// <summary>
/// click_count was created as integer (32-bit) while the application counts in long, so past about 2.1 billion
/// clicks the increment would fail with "integer out of range". ALTER COLUMN ... TYPE keeps NOT NULL and the
/// default (0).
/// Locking: integer -> bigint is not binary-compatible, so PostgreSQL rewrites the whole table under an
/// ACCESS EXCLUSIVE lock, blocking reads and writes until it finishes. That is instant for a small table; for a
/// large one, run it in a maintenance window or migrate through a new column instead.
/// </summary>
[Migration(2026112001)]
public class ClickCountToBigint : Migration
{
    public override void Up() =>
        Execute.Sql("ALTER TABLE short_urls ALTER COLUMN click_count TYPE bigint;");

    // Fails rather than truncating if a count no longer fits in integer.
    public override void Down() =>
        Execute.Sql("ALTER TABLE short_urls ALTER COLUMN click_count TYPE integer;");
}
