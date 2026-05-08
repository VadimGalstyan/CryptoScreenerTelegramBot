using Binance.Net.Clients;
using CryptoScreener.Data;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CryptoScreener.Clients.Implementations
{
    public class BinanceSpotClient : IExchangeClient
    {
        public string ExchangeName => "binancespot";

        private BinanceSocketClient _socketClient;
        private readonly Dictionary<string, DateTime> _lastUpdateTimes = new();
        private readonly object _timeLock = new object();

        public BinanceSpotClient()
        {
            _socketClient = new BinanceSocketClient(options =>
            {
                options.ReconnectPolicy = CryptoExchange.Net.Objects.ReconnectPolicy.FixedDelay;
                options.ReconnectInterval = TimeSpan.FromSeconds(5);
            });
        }

        public async void Start()
        {
            Console.WriteLine($"[System] starting connection {ExchangeName}...");

            var result = await _socketClient.SpotApi.ExchangeData.SubscribeToAllTickerUpdatesAsync(data =>
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
                Console.WriteLine($"[OK] {ExchangeName} successfully connected.");
            }
            else
            {
                Console.WriteLine($"[Error] {ExchangeName}: {result.Error}");
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
            Console.WriteLine($"[System] {ExchangeName} stopped.");
        }
    }
}