using FluentNHibernate.Mapping;
using UrlShortener.Model;

namespace UrlShortener.Repository
{
    public class ClickEventMap : ClassMap<ClickEvent>
    {
        public ClickEventMap()
        {
            Table("click_events");
            Id(x => x.Id).Column("id").GeneratedBy.Identity();
            Map(x => x.Code).Column("code").Not.Nullable();
            Map(x => x.ClickedAt).Column("clicked_at");
        }
    }
}
