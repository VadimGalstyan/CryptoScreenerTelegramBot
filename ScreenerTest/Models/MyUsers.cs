using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ScreenerTest.Services;

namespace ScreenerTest.Models
{
    internal class MyUsers
    {
        public long ChatId { get; set; }        // ID чата Telegram
        public int PercentChange { get; set; }  // Процент изменения
        public int TimeframeMinutes { get; set; } // Таймфрейм в минутах

        public int Step { get; set; } = 0;

        public PriceTracker PriceTracker { get; set; }
        public MyUsers(long chatId)
        {
            ChatId = chatId;
        }
    }
}
