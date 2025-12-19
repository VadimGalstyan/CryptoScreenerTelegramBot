using CryptoScreenerTestMonitor.Exchanges;
using ScreenerTest.Exchanges;
using ScreenerTest.Interfaces;
using ScreenerTest.Services;
using ScreenerTest.TelegramBot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ScreenerTest
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Host host = new Host();
            host.Start();


            decimal thresholdPercent = 1m; // Процент изменения
            TimeSpan timeframe = TimeSpan.FromMinutes(5);

            string connectionString = "Server=LAPTOP-EK1FURHH\\SQLEXPRESS;Database=ScreenerDB;Trusted_Connection=True;Encrypt=False;";
            var dbService = new DatabaseService(connectionString);

            var tracker = new PriceTracker(thresholdPercent, timeframe, 
                                            alert =>
                                            {
                                                dbService.SaveAlert(alert);
                                            },
                                            alert =>
                                            {
                                                _ = host.SendAlert(alert);    // отправляем alert в Telegram
                                            }
                                            )
            {
            };

            // Список клиентов всех бирж
            var clients = new List<Interfaces.IExchangeClient>
            {
                new BinanceClient(),
                //new BybitClient(),
                // new BitgetClient(),
                // new GateClient(),
                // new MexcClient()
            };

            foreach (var client in clients)
            {
                //client.OnPriceUpdate += tracker.AddPriceUpdate;
                client.OnPriceUpdate += async update =>
                {
                    await tracker.AddPriceUpdate(update, dbService);
                };

                await client.ConnectAsync();
                client.SubscribeToUSDTFuturesPairs();
            }

            _ = ClearOldAlerts(timeframe, dbService);

            Console.WriteLine("Monitoring USDT futures on all exchanges. Press any key to exit.");
            Console.ReadKey();
        }
 







        static async Task ClearOldAlerts(TimeSpan timeframe,DatabaseService dbService)
        {
            while (true)
            {
                await dbService.ClearOldAlerts();

                await Task.Delay(TimeSpan.FromMinutes(timeframe.Minutes));
            }
        }
    }
}
