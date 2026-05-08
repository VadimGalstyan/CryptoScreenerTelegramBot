using CryptoScreener.Clients;
using CryptoScreener.Data;
using Mexc.Net.Clients;

public class MexcFuturesClient : IExchangeClient
{
    public string ExchangeName => "mexcfutures";
    private MexcSocketClient _socketClient;
    private readonly Dictionary<string, DateTime> _lastUpdateTimes = new();
    private readonly object _timeLock = new object();

    public MexcFuturesClient()
    {
        _socketClient = new MexcSocketClient();
    }

    public async void Start()
    {
        Console.WriteLine($"[System] Starting {ExchangeName}...");

        try
        {
            var restClient = new MexcRestClient();
            var exchangeInfo = await restClient.FuturesApi.ExchangeData.GetSymbolsAsync();

            if (!exchangeInfo.Success) return;

            var symbols = exchangeInfo.Data
                .Where(s => s.Symbol.EndsWith("_USDT"))
                .Select(s => s.Symbol)
                .ToList();

  
            var tasks = symbols.Select(async symbol =>
            {
                var result = await _socketClient.FuturesApi.SubscribeToTickerUpdatesAsync(symbol, data =>
                {
                    string cleanSymbol = data.Data.Symbol.Replace("_", "");
                    float price = (float)data.Data.LastPrice;

                    if (ShouldUpdate(cleanSymbol))
                    {
                        MarketDataManager.Manager.UpdatePrice(ExchangeName, cleanSymbol, price);
                    }
                });

                await Task.Delay(50);
            });

            await Task.WhenAll(tasks);

            Console.WriteLine($"[OK] {ExchangeName} successfully connected to {symbols.Count} pairs.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Error] {ExchangeName}: {ex.Message}");
        }
    }

    private bool ShouldUpdate(string symbol)
    {
        lock (_timeLock)
        {
            var now = DateTime.Now;
            if (!_lastUpdateTimes.TryGetValue(symbol, out var last) || (now - last).TotalMilliseconds >= 1000)
            {
                _lastUpdateTimes[symbol] = now;
                return true;
            }
            return false;
        }
    }

    public void Stop() => _socketClient?.Dispose();
}