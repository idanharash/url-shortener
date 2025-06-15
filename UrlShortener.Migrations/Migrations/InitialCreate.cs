using FluentMigrator;

namespace UrlShortener.Migrations.Migrations;

[Migration(2025061501)]
public class InitialCreate : Migration
{
    public override void Up()
    {
        Create.Table("short_urls")
            .WithColumn("code").AsString().PrimaryKey()
            .WithColumn("original_url").AsString().NotNullable()
            .WithColumn("click_count").AsInt32().WithDefaultValue(0).NotNullable()
            .WithColumn("created_at").AsDateTime().NotNullable();
    }

    public override void Down()
    {
        Delete.Table("short_urls");
    }
}
