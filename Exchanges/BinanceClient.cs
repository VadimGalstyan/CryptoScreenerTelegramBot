using ScreenerTest.Interfaces;
using ScreenerTest.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;


namespace ScreenerTest.Exchanges
{
    public class BinanceClient : IExchangeClient
    {
        private readonly Uri _url = new Uri("wss://fstream.binance.com/ws/!ticker@arr");
        private ClientWebSocket _ws;

        public event Action<PriceUpdate> OnPriceUpdate;

        public async Task ConnectAsync()
        {
            _ws = new ClientWebSocket();
            await _ws.ConnectAsync(_url, CancellationToken.None);
            Console.WriteLine("Binance connected.");
            _ = ReceiveMessages();
        }

        public void SubscribeToUSDTFuturesPairs()
        {
            // Binance stream уже возвращает все тикеры USDT-фьючерсов через "!ticker@arr"
            Console.WriteLine("Subscribed to Binance USDT futures tickers.");
        }

        private async Task ReceiveMessages()
        {
            var buffer = new byte[8192];

            while (_ws.State == WebSocketState.Open)
            {
                var ms = new MemoryStream();
                WebSocketReceiveResult result;

                do
                {
                    result = await _ws.ReceiveAsync(buffer, CancellationToken.None);
                    ms.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var json = Encoding.UTF8.GetString(ms.ToArray());
                    ProcessMessage(json);
                }
            }
        }


        private void ProcessMessage(string json)
        {
           
            try
            {
                var tickers = JsonSerializer.Deserialize<JsonElement[]>(json);

                foreach (var ticker in tickers)
                {
                    if (!ticker.GetProperty("s").GetString().EndsWith("USDT"))
                        continue;

                    var pair = ticker.GetProperty("s").GetString();
                    var priceString = ticker.GetProperty("c").GetString();

                    if (!decimal.TryParse(priceString, NumberStyles.Any, CultureInfo.InvariantCulture, out var price))
                    {
                        Console.WriteLine($"Can't parse price: {priceString}");
                        continue;
                    }

                    OnPriceUpdate?.Invoke(new PriceUpdate
                    {
                        Exchange = "Binance",
                        Pair = pair,
                        Price = price,
                        Time = DateTime.UtcNow

                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("here3: " + ex.Message);
            }

        }

    }
}


