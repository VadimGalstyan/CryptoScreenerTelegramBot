using Bybit.Net.Clients;
using CryptoScreener.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CryptoScreener.Clients.Implementations
{
    public class BybitSpotClient : IExchangeClient
    {
        public string ExchangeName => "bybitspot";
        private BybitSocketClient _socketClient;
        private readonly Dictionary<string, DateTime> _lastUpdateTimes = new();
        private readonly object _timeLock = new object();

        public BybitSpotClient()
        {
            _socketClient = new BybitSocketClient(options =>
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
                var restClient = new BybitRestClient();
                var exchangeInfo = await restClient.V5Api.ExchangeData.GetSpotSymbolsAsync();
                if (!exchangeInfo.Success) return;

                var symbols = exchangeInfo.Data.List
                    .Where(s => s.Name.EndsWith("USDT"))
                    .Select(s => s.Name)
                    .ToList();

                var chunks = symbols.Chunk(10);

                foreach (var chunk in chunks)
                {
                    var result = await _socketClient.V5SpotApi.SubscribeToTickerUpdatesAsync(chunk, data =>
                    {
                        string symbol = data.Data.Symbol;
                        float price = (float)data.Data.LastPrice;

                        if (ShouldUpdate(symbol))
                        {
                            MarketDataManager.Manager.UpdatePrice(ExchangeName, symbol, price);
                        }
                    });

                    if (!result.Success)
                        Console.WriteLine($"[Error] {ExchangeName}: {result.Error}");

                    await Task.Delay(50);
                }

                Console.WriteLine($"[OK] {ExchangeName} successfully connected.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Critical error] {ExchangeName}: {ex.Message}");
            }
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

        public void Stop()
        {
            _socketClient?.Dispose();
        }
    }
}