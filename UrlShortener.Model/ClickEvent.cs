namespace UrlShortener.Model
{
    public class ClickEvent
    {
        public virtual long Id { get; set; }
        public virtual string Code { get; set; } = null!;
        public virtual DateTime ClickedAt { get; set; } = DateTime.UtcNow;
    }
}
