using FluentNHibernate.Mapping;
using UrlShortener.Model;

namespace UrlShortener.Repository
{
    public class ShortUrlMap : ClassMap<ShortUrl>
    {
        public ShortUrlMap()
        {
            Table("short_urls");
            Id(x => x.Code).Column("code").GeneratedBy.Assigned();
            Map(x => x.OriginalUrl).Column("original_url").Not.Nullable();
            Map(x => x.CreatedAt).Column("created_at");
            Map(x => x.ClickCount).Column("click_count");
        }
    }
}
