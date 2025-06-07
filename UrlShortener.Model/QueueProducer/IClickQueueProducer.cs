namespace UrlShortener.Model.QueueProducer
{
    public interface IClickQueueProducer
    {
        void SendClick(string code);
    }
}
