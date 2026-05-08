using Bitget.Net.Clients;
using Bitget.Net.Enums; 
using CryptoScreener.Data;
using System;
using System.Collections.Generic;

namespace CryptoScreener.Clients.Implementations
{
    public class BitgetFuturesClient : IExchangeClient
    {
        public string ExchangeName => "bitgetfutures";
        private BitgetSocketClient _socketClient;
        private readonly Dictionary<string, DateTime> _lastUpdateTimes = new();
        private readonly object _timeLock = new object();

        public BitgetFuturesClient()
        {
            _socketClient = new BitgetSocketClient(options =>
            {
                options.ReconnectPolicy = CryptoExchange.Net.Objects.ReconnectPolicy.FixedDelay;
                options.ReconnectInterval = TimeSpan.FromSeconds(5);
            });
        }

        public async void Start()
        {
            Console.WriteLine($"[System] start {ExchangeName}...");
            try
            {
                var restClient = new Bitget.Net.Clients.BitgetRestClient();

                var exchangeInfo = await restClient.FuturesApiV2.ExchangeData.GetContractsAsync(BitgetProductTypeV2.UsdtFutures);

                if (!exchangeInfo.Success)
                {
                    Console.WriteLine($"[Error] cannot get contracts Bitget Futures: {exchangeInfo.Error}");
                    return;
                }

                var symbols = exchangeInfo.Data
                    .Where(s => s.Symbol.EndsWith("USDT"))
                    .Select(s => s.Symbol)
                    .ToList();

                var chunks = symbols.Chunk(10);

                foreach (var chunk in chunks)
                {
                    var result = await _socketClient.FuturesApiV2.SubscribeToTickerUpdatesAsync(
                        BitgetProductTypeV2.UsdtFutures,
                        chunk,
                        data =>
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

                    if (!result.Success)
                        Console.WriteLine($"[Subscription error] {ExchangeName} : {result.Error}");

                    await Task.Delay(100);
                }

                Console.WriteLine($"[OK] {ExchangeName} connected to {symbols.Count} pairs.");
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

        public void Stop() => _socketClient?.Dispose();
    }
}