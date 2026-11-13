namespace UrlShortener.Model.QueueProducer;

public interface IClickQueueProducer
{
    /// <summary>
    /// Records a click without waiting for the broker. Never throws: click tracking must not break a redirect.
    /// Returns false if the click was dropped because the local buffer is full.
    /// </summary>
    bool EnqueueClick(string code);
}
