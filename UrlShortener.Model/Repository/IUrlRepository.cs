namespace UrlShortener.Model.Repository;

public interface IUrlRepository
{
    Task<ShortUrl?> GetByCode(string code);

    /// <summary>Inserts a new short URL. Returns null if the code is already taken.</summary>
    Task<ShortUrl?> TryCreateAsync(string code, string originalUrl);

    /// <summary>Atomically increments the click counter; a no-op for unknown codes.</summary>
    Task IncrementClickCountAsync(string code);
}
