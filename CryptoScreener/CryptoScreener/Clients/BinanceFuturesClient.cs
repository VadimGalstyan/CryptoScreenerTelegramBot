using Binance.Net.Clients;
using CryptoScreener.Data;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace CryptoScreener.Clients.Implementations
{
    public class BinanceFuturesClient : IExchangeClient
    {
        public string ExchangeName => "binancefutures";

        private BinanceSocketClient _socketClient;
        private readonly Dictionary<string, DateTime> _lastUpdateTimes = new();
        private readonly object _timeLock = new object();

        public BinanceFuturesClient()
        {
            _socketClient = new BinanceSocketClient(options =>
            {
                options.ReconnectPolicy = CryptoExchange.Net.Objects.ReconnectPolicy.FixedDelay;
                options.ReconnectInterval = TimeSpan.FromSeconds(5);

            });
        }

        public async void Start()
        {
            Console.WriteLine($"[System] starting {ExchangeName}...");

            var result = await _socketClient.UsdFuturesApi.ExchangeData.SubscribeToAllTickerUpdatesAsync(data =>
            {
                foreach (var ticker in data.Data)
                {
                    string symbol = ticker.Symbol;
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
        }
    }
}