using Bitget.Net.Clients;
using CryptoScreener.Data;
using System;
using System.Collections.Generic;

namespace CryptoScreener.Clients.Implementations
{
    public class BitgetSpotClient : IExchangeClient
    {
        public string ExchangeName => "bitgetspot";
        private BitgetSocketClient _socketClient;
        private readonly Dictionary<string, DateTime> _lastUpdateTimes = new();
        private readonly object _timeLock = new object();

        public BitgetSpotClient()
        {
            _socketClient = new BitgetSocketClient(options =>
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
                var restClient = new Bitget.Net.Clients.BitgetRestClient();
                var exchangeInfo = await restClient.SpotApiV2.ExchangeData.GetSymbolsAsync();

                if (!exchangeInfo.Success)
                {
                    Console.WriteLine($"[Error] Can not get Bitget Spot: {exchangeInfo.Error}");
                    return;
                }

                var symbols = exchangeInfo.Data
                    .Where(s => s.Symbol.EndsWith("USDT") && s.Status == Bitget.Net.Enums.SymbolStatus.Online)
                    .Select(s => s.Symbol)
                    .ToList();

                var chunks = symbols.Chunk(10);

                foreach (var chunk in chunks)
                {
                    var result = await _socketClient.SpotApiV2.SubscribeToTickerUpdatesAsync(chunk, data =>
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
                        Console.WriteLine($"[Subscripton Error] {ExchangeName} : {result.Error}");

                    await Task.Delay(100);
                }

                Console.WriteLine($"[OK] {ExchangeName} Connected {symbols.Count} pairs.");
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