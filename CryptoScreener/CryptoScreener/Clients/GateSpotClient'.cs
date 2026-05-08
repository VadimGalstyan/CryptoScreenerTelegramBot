using GateIo.Net.Clients;
using GateIo.Net.Enums;
using CryptoScreener.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CryptoScreener.Clients.Implementations
{
    public class GateSpotClient : IExchangeClient
    {
        public string ExchangeName => "gatespot";
        private GateIoSocketClient _socketClient;
        private readonly Dictionary<string, DateTime> _lastUpdateTimes = new();
        private readonly object _timeLock = new object();

        public GateSpotClient()
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
                var exchangeInfo = await restClient.SpotApi.ExchangeData.GetSymbolsAsync();

                if (!exchangeInfo.Success) return;

                var symbols = exchangeInfo.Data
                    .Where(s => s.Name.EndsWith("_USDT") && s.TradeStatus == SymbolStatus.Tradable)
                    .Select(s => s.Name)
                    .ToList();

                var chunks = symbols.Chunk(10);
                foreach (var chunk in chunks)
                {
                    await _socketClient.SpotApi.SubscribeToTickerUpdatesAsync(chunk, data =>
                    {
                        string symbol = data.Data.Symbol.Replace("_", "");
                        float price = (float)data.Data.LastPrice;

                        if (ShouldUpdate(symbol))
                        {
                            MarketDataManager.Manager.UpdatePrice(ExchangeName, symbol, price);
                        }
                    });
                    await Task.Delay(100);
                }
                Console.WriteLine($"[OK] {ExchangeName} connected to {symbols.Count} pairs.");
            }
            catch (Exception ex) { Console.WriteLine($"[Error Gate Spot] {ex.Message}"); }
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