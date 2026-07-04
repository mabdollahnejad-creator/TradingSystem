using TradingSystem.Domain.Entities;

public class WatchlistCryptocurrency
{
    public int Id { get; set; }

    public int WatchlistId { get; set; }

    public int CryptocurrencyId { get; set; }

    public Watchlist Watchlist { get; set; } = null!;

    public Cryptocurrency Cryptocurrency { get; set; } = null!;
}