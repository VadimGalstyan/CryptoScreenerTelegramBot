using Bybit.Net.Clients;
using CryptoScreener.Data;
using System;
using System.Collections.Generic;

namespace CryptoScreener.Clients.Implementations
{
    public class BybitFuturesClient : IExchangeClient
    {
        public string ExchangeName => "bybitfutures";
        private BybitSocketClient _socketClient;
        private readonly Dictionary<string, DateTime> _lastUpdateTimes = new();
        private readonly object _timeLock = new object();

        public BybitFuturesClient()
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
                var exchangeInfo = await restClient.V5Api.ExchangeData.GetLinearInverseSymbolsAsync(Bybit.Net.Enums.Category.Linear);
                if (!exchangeInfo.Success) return;

                var symbols = exchangeInfo.Data.List
                    .Where(s => s.Name.EndsWith("USDT"))
                    .Select(s => s.Name)
                    .ToList();

                var chunks = symbols.Chunk(10);

                foreach (var chunk in chunks)
                {
                    var result = await _socketClient.V5LinearApi.SubscribeToTickerUpdatesAsync(chunk, data =>
                    {
                        string symbol = data.Data.Symbol;
                        if (data.Data.LastPrice != null)
                        {
                            float price = (float)data.Data.LastPrice;
                            if (ShouldUpdate(symbol))
                            {
                                MarketDataManager.Manager.UpdatePrice(ExchangeName, symbol, price);
                            }
                        }
                    });
                    await Task.Delay(50);
                }
                Console.WriteLine($"[OK] {ExchangeName} seccessfully started.");
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

        public void Stop()
        {
            _socketClient?.Dispose();
        }
    }
}