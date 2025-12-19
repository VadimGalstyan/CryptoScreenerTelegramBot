using CryptoScreenerTestMonitor.Exchanges;
using ScreenerTest.Exchanges;
using ScreenerTest.Interfaces;
using ScreenerTest.Services;
using ScreenerTest.TelegramBot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Bot.Types;

namespace ScreenerTest
{
    internal class Program
    {
        public static List<Interfaces.IExchangeClient> Clients = new List<Interfaces.IExchangeClient>();

        static async Task Main(string[] args)
        {
            string connectionString = "Server=LAPTOP-EK1FURHH\\SQLEXPRESS;Database=ScreenerDB;Trusted_Connection=True;Encrypt=False;";
            DatabaseService? dbService = new DatabaseService(connectionString);

            Host host = new Host(dbService);
            host.Start();

            // Список клиентов всех бирж
            Clients.Add(new BinanceClient());
            //Clients.Add(new BybitClient());
            //Clients.Add(new BitgetClient());
            //Clients.Add(new GateClient());
            //Clients.Add(new MexcClient());

            foreach (var client in Clients)
            {
                await client.ConnectAsync();
                client.SubscribeToUSDTFuturesPairs();

            }


            Console.WriteLine("Monitoring USDT futures on all exchanges. Press any key to exit.");
            Console.ReadKey();
        } 
    }
}
