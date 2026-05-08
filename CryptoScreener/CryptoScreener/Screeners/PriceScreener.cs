using CryptoScreener.Data;
using CryptoScreener.TelegramBot;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types;
using static System.Net.Mime.MediaTypeNames;

namespace CryptoScreener.Screeners
{
    public class PriceScreener : IDisposable
    {
        public short _percent { get; set; }
        public short _timeframe { get; set; }//minutes

        private readonly ConcurrentDictionary<string, DateTime> _alertsHistory = new();
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private bool _isRunning = true;
        public PriceScreener(short percent, short timeframe)
        {
            _percent = percent;
            _timeframe = timeframe;
            StartCleanupLoop();
        }

        public void SetPercent(short percent)
        {
            _percent = percent;
            _alertsHistory.Clear();
        }

        public void SetTimeframe(short timeframe)
        {
            _timeframe = timeframe;
            _alertsHistory.Clear();
        }
        private void StartCleanupLoop()
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    while (!_cts.Token.IsCancellationRequested)
                    {
                        await Task.Delay(TimeSpan.FromMinutes(1), _cts.Token);

                        int expiryMinutes = _timeframe * 3;
                        var now = DateTime.Now;

                        var toRemove = _alertsHistory
                            .Where(x => (now - x.Value).TotalMinutes >= expiryMinutes)
                            .Select(x => x.Key)
                            .ToList();

                        foreach (var key in toRemove)
                            _alertsHistory.TryRemove(key, out _);
                    }
                }
                catch (OperationCanceledException) {  }
                catch (Exception ex) { Console.WriteLine($"[Cleanup Error]: {ex.Message}"); }
            }, _cts.Token);
        }
        public async void CheckCoinditions(string exchange, string pair, PriceBuffer buffer, int _chatId)
        {
            float currentPrice = buffer.GetCurrentPrice();
            float lastPrice = buffer.GetPrice(_timeframe);

            if (lastPrice <= 0) return;

            float change = ((currentPrice - lastPrice) / lastPrice) * 100;
            if (Math.Abs(change) >= _percent)
            {
                if (_alertsHistory.TryGetValue(pair, out DateTime lastAlertTime))
                {
                    if ((DateTime.Now - lastAlertTime).TotalMinutes < 10) return;
                }

                _alertsHistory[pair] = DateTime.Now;

                string direction = change > 0 ? "📈 ПАМП" : "📉 ДАМП";
                string message = $"{direction}\n" +
                                 $"Биржа: {exchange.ToUpper()}\n" +
                                 $"Пара: {pair}\n" +
                                 $"Изменение: {change:F2}%\n" +
                                 $"Таймфрейм: {_timeframe} мин.\n" +
                                 $"Цена: {currentPrice}";

                _ = TelegramHost.SendAlert(_chatId, message);
            }
        }

        public void Dispose()
        {
            _cts.Cancel(); 
            _cts.Dispose();
            _alertsHistory.Clear();
        }
    }
}