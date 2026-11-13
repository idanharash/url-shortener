using FluentNHibernate.Mapping;
using NHibernate.Type;
using UrlShortener.Model;

namespace UrlShortener.Infrastructure.Persistence.Mappings;

public class ShortUrlMap : ClassMap<ShortUrl>
{
    public ShortUrlMap()
    {
        Table("short_urls");
        Id(x => x.Code).Column("code").GeneratedBy.Assigned();
        Map(x => x.OriginalUrl).Column("original_url").Not.Nullable();
        // Stored as UTC; UtcDateTimeType marks values read back as DateTimeKind.Utc so the API returns "...Z".
        Map(x => x.CreatedAt).Column("created_at").CustomType<UtcDateTimeType>();
        Map(x => x.ClickCount).Column("click_count");
    }
}
