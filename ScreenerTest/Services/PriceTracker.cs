using ScreenerTest.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Bot.Types;

namespace ScreenerTest.Services
{
    public class PriceTracker
    {
        private readonly long chatId; //Kajdi user imeet v sebe obyekt PriceTracker,i dlya proverki cherez db nujen chatid,poetomu zdec est eto pole

        private readonly decimal _thresholdPercent;
        private readonly TimeSpan _timeframe;
        private readonly int _timeframeOnMinutes;
        private readonly Dictionary<string, LinkedList<PriceUpdate>> _priceHistory = new();
        private readonly Action<Alert> _onAlert;
        private readonly Action<Alert> _sendAlert;


        public PriceTracker(long chatid,decimal thresholdPercent, TimeSpan timeframe, Action<Alert> onAlert = null, Action<Alert> sendAlert = null)
        {
            chatId = chatid;
            _thresholdPercent = thresholdPercent;
            _timeframe = timeframe;
            _timeframeOnMinutes = timeframe.Minutes;
            _onAlert = onAlert;
            _sendAlert = sendAlert;
        }

        public PriceTracker(Action<Alert> onAlert = null, Action<Alert> sendAlert = null)
        {

            _onAlert = onAlert;
            _sendAlert = sendAlert;
        }

        public async Task AddPriceUpdate(PriceUpdate update,DatabaseService dbService)
        {
            if (!_priceHistory.ContainsKey(update.Pair))
                _priceHistory[update.Pair] = new LinkedList<PriceUpdate>();

            var list = _priceHistory[update.Pair];
            list.AddLast(update);

            // Удаляем старые элементы вне timeframe
            while (list.First != null && list.First.Value.Time < DateTime.UtcNow - _timeframe)
                list.RemoveFirst();

            if (list.First == null) return;

            var oldest = list.First.Value.Price;
            var newest = list.Last.Value.Price;

            if (oldest == 0) return;

            var changePercent = ((newest - oldest) / oldest) * 100;


            if (Math.Abs(changePercent) >= _thresholdPercent)
            {
                var alert = new Alert
                {
                    Exchange = update.Exchange,
                    Pair = update.Pair,
                    ChangePercent = changePercent,
                    Time = DateTime.UtcNow,
                    Timeframe = _timeframeOnMinutes
                };

                if (!(await dbService.ContainingAlert(chatId,update.Pair)))
                {
                    Console.WriteLine($"ALERT: {alert.Exchange} {alert.Pair} changed {alert.ChangePercent:F2}% in last {alert.Timeframe}");
                    _onAlert?.Invoke(alert);
                    _sendAlert?.Invoke(alert);
                }

            }
        }
    }
}

