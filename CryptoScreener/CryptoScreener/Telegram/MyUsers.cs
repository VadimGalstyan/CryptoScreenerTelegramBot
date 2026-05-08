using CryptoScreener.Data;
using CryptoScreener.Screeners;
using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoScreener.Telegram
{
    public class MyUsers
    {
        private readonly int _chatId;
        public int Step { get; set; } = 0;
        public string SelectedExchange { get; set; } = "";
        public string SelectedMarket { get; set; } = "";
        public string SelectedMode { get; set; } = "";
        public short TempPercent { get; set; } = 5;
        public short TempTimeframe { get; set; } = 5;
        public string GetKey() => (SelectedExchange + SelectedMarket).ToLower();

        public Dictionary<string, PriceScreener?> _priceScreener = new()
        {
            {"binancespot", null },
            {"binancefutures",null},
            {"bybitspot",null},
            {"bybitfutures",null},
            {"bitgetspot",null},
            {"bitgetfutures",null},
            {"gatespot",null},
            {"gatefutures",null},
            {"mexcspot",null},
            {"mexcfutures",null}
        };

        public Dictionary<string, VolumeScreener?> _volumeScreener = new()
        {
            {"binancespot", null },
            {"binancefutures",null},
            {"bybitspot",null},
            {"bybitfutures",null},
            {"bitgetspot",null},
            {"bitgetfutures",null},
            {"gatespot",null},
            {"gatefutures",null},
            {"mexcspot",null},
            {"mexcfutures",null}
        };

        public Dictionary<string, ArbitrageScreener?> _arbitrageScreener = new()
        {
            {"binancespot", null },
            {"binancefutures",null},
            {"bybitspot",null},
            {"bybitfutures",null},
            {"bitgetspot",null},
            {"bitgetfutures",null},
            {"gatespot",null},
            {"gatefutures",null},
            {"mexcspot",null},
            {"mexcfutures",null}
        };

        public MyUsers(int chatId)
        {
            _chatId = chatId;
        }

        public void TurnOnPriceScreener(string exchange, short percent, short timeframe)
        {
            if (_priceScreener[exchange] != null) return;
            _priceScreener[exchange] = new PriceScreener(percent, timeframe);
            Console.WriteLine($"turn on {exchange} {percent} {timeframe}");
        }
        public void TurnOnVolumeScreener(string exchange, short percent, short timeframe)
        {
            if(_volumeScreener[exchange] != null) return;
            _volumeScreener[exchange] = new VolumeScreener(percent);
        }
        public void TurnOnArbitrageScreener(string exchange, short percent, short timeframe)
        {
            if(_arbitrageScreener[exchange] != null) return;
            _arbitrageScreener[exchange] = new ArbitrageScreener();
        }

        public void ActivatePriceScreener()
        {
            foreach (var value in _priceScreener)
            {
                string exchange = value.Key;
                var screener = value.Value;
                if (screener == null) continue;

                if (MarketDataManager.Manager._exchanges.TryGetValue(exchange, out var pairs))
                {
                    foreach (var pair in pairs)
                    {
                        string symbol = pair.Key;
                        PriceBuffer buffer = pair.Value;

                        screener.CheckCoinditions(exchange, symbol, buffer,_chatId);
                    }
                }
            }
        }

        public void ActivateVolumeScreener() {}
        public void ActivateArbitrageScreener() {}
    }
}
