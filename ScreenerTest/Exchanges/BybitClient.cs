using ScreenerTest.Interfaces;
using ScreenerTest.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
  
    
namespace CryptoScreenerTestMonitor.Exchanges
{
    public class BybitClient : IExchangeClient
    {
        private readonly Uri _url = new Uri("wss://stream.bybit.com/v5/public/linear");
        private ClientWebSocket _ws;

        public event Action<PriceUpdate> OnPriceUpdate;

        public async Task ConnectAsync()
        {
            _ws = new ClientWebSocket();
            await _ws.ConnectAsync(_url, CancellationToken.None);
            Console.WriteLine("Bybit connected.");
            _ = ReceiveMessages();
        }

        public void SubscribeToUSDTFuturesPairs()
        {
            var subscribeMessage = new
            {
                op = "subscribe",
                args = new object[]
                {
                    new
                    {
                        instType = "USDT-FUTURES",
                        channel = "ticker",
                        instId = "BTCUSDT"
                    }
                }
            };
            var json = JsonSerializer.Serialize(subscribeMessage);
            _ = SendMessageAsync(json);
        }

        private async Task SendMessageAsync(string message)
        {
            var buffer = Encoding.UTF8.GetBytes(message);
            await _ws.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private async Task ReceiveMessages()
        {
            var buffer = new byte[8192];
            while (_ws.State == WebSocketState.Open)
            {
                var result = await _ws.ReceiveAsync(buffer, CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    ProcessMessage(json);
                }
            }
        }

        private void ProcessMessage(string json)
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("arg", out var arg) && arg.GetProperty("channel").GetString() == "ticker")
                {
                    var data = root.GetProperty("data")[0];
                    var price = data.GetProperty("lastPrice").GetDecimal();
                    var pair = data.GetProperty("instId").GetString();

                    OnPriceUpdate?.Invoke(new PriceUpdate
                    {
                        Exchange = "Bybit",
                        Pair = pair,
                        Price = price,
                        Time = DateTime.UtcNow
                    });
                }
            }
            catch { }
        }
    }
}
