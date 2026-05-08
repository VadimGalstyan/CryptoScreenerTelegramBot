using CryptoScreener.Clients;
using CryptoScreener.Data;
using GateIo.Net.Clients;

public class GateFuturesClient : IExchangeClient
{
    public string ExchangeName => "gatefutures";
    private GateIoSocketClient _socketClient;
    private readonly Dictionary<string, DateTime> _lastUpdateTimes = new();
    private readonly object _timeLock = new object();

    public GateFuturesClient()
    {
        _socketClient = new GateIoSocketClient(options =>
        {
            options.ReconnectPolicy = CryptoExchange.Net.Objects.ReconnectPolicy.FixedDelay;
            options.ReconnectInterval = TimeSpan.FromSeconds(5);
        });
    }

    public async void Start()
    {
        Console.WriteLine($"[System] Start {ExchangeName}...");
        try
        {
            var restClient = new GateIoRestClient();
            var exchangeInfo = await restClient.PerpetualFuturesApi.ExchangeData.GetContractsAsync("usdt");

            if (!exchangeInfo.Success) return;

            var symbols = exchangeInfo.Data.Select(s => s.Name).ToList();
            var chunks = symbols.Chunk(10);

            foreach (var chunk in chunks)
            {
                await _socketClient.PerpetualFuturesApi.SubscribeToTickerUpdatesAsync("usdt", chunk, data =>
                {
                    foreach (var ticker in data.Data)
                    {
                        string symbol = ticker.Contract.Replace("_", "");

                        float price = (float)ticker.LastPrice;

                        if (ShouldUpdate(symbol))
                        {
                            MarketDataManager.Manager.UpdatePrice(ExchangeName, symbol, price);
                        }
                    }
                });
                await Task.Delay(100);
            }
            Console.WriteLine($"[OK] {ExchangeName} connected to {symbols.Count} pairs.");
        }
        catch (Exception ex) { Console.WriteLine($"[Error Gate Futures] {ex.Message}"); }
    }

    private bool ShouldUpdate(string symbol)
    {
        lock (_timeLock)
        {
            var now = DateTime.Now;
            if (!_lastUpdateTimes.TryGetValue(symbol, out var last))
            {
                _lastUpdateTimes[symbol] = now;
                return true;
            }
            if ((now - last).TotalMilliseconds >= 1000)
            {
                _lastUpdateTimes[symbol] = now;
                return true;
            }
            return false;
        }
    }

    public void Stop() => _socketClient?.Dispose();
}