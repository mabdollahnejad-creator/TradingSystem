public class Watchlist
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateTime CreatedDate { get; set; }
        = DateTime.UtcNow;

    public ICollection<WatchlistCryptocurrency>
        WatchlistCryptocurrencies
    { get; set; }
            = new List<WatchlistCryptocurrency>();
}