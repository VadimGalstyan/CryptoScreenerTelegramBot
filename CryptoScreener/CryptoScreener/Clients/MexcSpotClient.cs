using Mexc.Net.Clients;
using CryptoScreener.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CryptoScreener.Clients.Implementations
{
    public class MexcSpotClient : IExchangeClient
    {
        public string ExchangeName => "mexcspot";
        private MexcSocketClient _socketClient;
        private readonly Dictionary<string, DateTime> _lastUpdateTimes = new();
        private readonly object _timeLock = new object();

        public MexcSpotClient()
        {
            _socketClient = new MexcSocketClient(options =>
            {
                options.ReconnectPolicy = CryptoExchange.Net.Objects.ReconnectPolicy.FixedDelay;
                options.ReconnectInterval = TimeSpan.FromSeconds(5);
            });
        }

        public async void Start()
        {
            Console.WriteLine($"[System] Starting {ExchangeName}...");

            try
            {
                var result = await _socketClient.SpotApi.SubscribeToAllMiniTickerUpdatesAsync(data =>
                {
                    foreach (var ticker in data.Data)
                    {
                        string symbol = ticker.Symbol;

                        if (!symbol.EndsWith("USDT")) continue;

                        float price = (float)ticker.LastPrice;

                        if (ShouldUpdate(symbol))
                        {
                            MarketDataManager.Manager.UpdatePrice(ExchangeName, symbol, price);
                        }
                    }
                });

                if (result.Success)
                {
                    Console.WriteLine($"[OK] {ExchangeName} successfully connected to all pairs.");
                }
                else
                {
                    Console.WriteLine($"[Error] {ExchangeName}: {result.Error}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Critical Error] {ExchangeName}: {ex.Message}");
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

        public void Stop() => _socketClient?.Dispose();
    }
}