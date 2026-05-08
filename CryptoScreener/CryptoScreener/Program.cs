using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CryptoScreener.Clients;
using CryptoScreener.Clients.Implementations;
using CryptoScreener.TelegramBot;

namespace CryptoScreener
{
    public class Program
    {
        public static TelegramHost BotHost;
        private static List<IExchangeClient> _exchangeClients = new List<IExchangeClient>();

        public static async Task Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.Title = "CryptoScreener PRO System";

            Console.WriteLine("======================================");
            Console.WriteLine("   CRYPTO SCREENER SERVER STARTING    ");
            Console.WriteLine("======================================");

            try
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Initializing Telegram Host...");
                BotHost = new TelegramHost();

                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Connecting to WebSockets...");

                _exchangeClients.Add(new BinanceFuturesClient());
                _exchangeClients.Add(new BinanceSpotClient());
                _exchangeClients.Add(new BybitFuturesClient());
                _exchangeClients.Add(new BybitSpotClient());
                _exchangeClients.Add(new BitgetFuturesClient());
                _exchangeClients.Add(new BitgetSpotClient());
                _exchangeClients.Add(new GateFuturesClient());
                _exchangeClients.Add(new GateSpotClient());
                _exchangeClients.Add(new MexcFuturesClient());
                _exchangeClients.Add(new MexcSpotClient());


                foreach (var client in _exchangeClients)
                {
                    client.Start();
                }

                BotHost.Start();
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Telegram Bot is Online.");

                _ = Task.Run(async () =>
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Background Monitoring Started.");

                    while (true)
                    {
                        try
                        {
                            foreach (var user in BotHost._userSettings.Values.ToList()) 
                            {
                                user.ActivatePriceScreener();
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[Loop Error]: {ex.Message}");
                        }

                        await Task.Delay(1000);
                    }
                });

                Console.WriteLine("======================================");
                Console.WriteLine("SERVER RUNNING. Press [ENTER] to stop.");
                Console.WriteLine("======================================");

                Console.ReadLine();

                foreach (var client in _exchangeClients) client.Stop();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[FATAL ERROR]: {ex.Message}");

                if (ex.InnerException != null)
                    Console.WriteLine($"[INNER ERROR]: {ex.InnerException.Message}");
                Console.ResetColor();
                Console.WriteLine("Press Enter to close...");
                Console.ReadLine();
            }
        }
    }
}