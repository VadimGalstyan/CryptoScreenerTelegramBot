using ScreenerTest.Models;
using ScreenerTest.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using static System.Net.Mime.MediaTypeNames;
using ScreenerTest.Services;

namespace ScreenerTest.TelegramBot
{
    class Host
    {
     
        private Dictionary<long, CancellationTokenSource> _oldAlertsToken = new();

        private static string _token { get; set; } = "Your token";
        private DatabaseService _dbService;

        // key = chatId, value = UserSettings
        public Dictionary<long, MyUsers> _userSettings = new Dictionary<long, MyUsers>();
        public static TelegramBotClient? _client;

        public Host(DatabaseService db)
        {
            _client = new TelegramBotClient(_token);
            _dbService = db;
        }

        public void Start()
        {
            Console.WriteLine("Bot is ran");
            _client.StartReceiving(UpdateHandler, ErrorHandler);
        }
        private async Task ErrorHandler(ITelegramBotClient client, Exception exception, HandleErrorSource source, CancellationToken token)
        {
            Console.WriteLine($"Error: {exception.Message}");
            await Task.CompletedTask;
        }


        private async Task UpdateHandler(ITelegramBotClient client, Update update, CancellationToken token)
        {
            long chatId = 0;
            string text = "";

            // ---------- Шаг 0: старт команды ----------
            if (update.Message != null)
            {
                chatId = update.Message.Chat.Id;
                text = update.Message.Text ?? "";
            }
            else if (update.CallbackQuery != null && update.CallbackQuery.Message != null)
            {
                chatId = update.CallbackQuery.Message.Chat.Id;
                // callback обычно не имеет text, но мы можем читать query.Data при необходимости
            }


            if (update.Message != null && text == "/start")
            {
                if (!_userSettings.ContainsKey(chatId))
                {
                    _userSettings[chatId] = new MyUsers(chatId);
                }

                var keyboard = new InlineKeyboardMarkup(new[]
                {
                    new[] { InlineKeyboardButton.WithCallbackData("Начать мониторинг", "start_monitoring") },
                    new[] { InlineKeyboardButton.WithCallbackData("Помощь", "help") }
                });


                await client.SendMessage(chatId, "🤖 Бот готов! Выберите действие:", replyMarkup: keyboard);
                _userSettings[chatId].Step++;
                return;
            }


            // ---------- Шаг 1: обработка нажатия кнопок ----------
            if (update.CallbackQuery is { } query && _userSettings[chatId].Step == 1)
            {
                if (query.Data == "start_monitoring")
                {
                    await client.SendMessage(query.Message.Chat.Id, "✏️ Введите процент изменения:");
                    _userSettings[chatId].Step++;
                    return;
                }

                if (query.Data == "help")
                {
                    await client.SendMessage(query.Message.Chat.Id, "ℹ️ Этот бот отслеживает сигналы и уведомляет вас о процентных изменениях.");
                    //_userSettings[chatId].Step += 2; // пропускаем шаг ввода процента
                    return;
                }
            }

            // ---------- Шаг "Change" ----------
            if (update.Message?.Text == "Change")
            {
                _userSettings[chatId].Step = 2;
            }

            // ---------- Шаг 2: ввод процента ----------
            if (_userSettings[chatId].Step == 2 && update.Message != null)
            {


                if (int.TryParse(text.Replace("%", ""), out int percent))
                {
                    if (!_userSettings.ContainsKey(chatId))
                    {
                        _userSettings[chatId] = new MyUsers(chatId);
                    }

                    _userSettings[chatId].PercentChange = percent;

                    string emoji = percent >= 0 ? "📈" : "📉";
                    await client.SendMessage(chatId, $"{emoji} Пользователь ввёл процент изменения: {percent}%");
                    await client.SendMessage(chatId, "⏱ Введите таймфрейм изменения в минутах:");
                    _userSettings[chatId].Step++;
                }
                else
                {
                    await client.SendMessage(chatId, "⚠️ Пожалуйста, введите число в формате процента, например 12");
                }
                return;
            }

            // ---------- Шаг 3: ввод таймфрейма ----------
            if (_userSettings[chatId].Step == 3 && update.Message != null)
            {


                if (int.TryParse(text.Replace("м", ""), out int timeframeMinutes) && timeframeMinutes > 0)
                {
                    _userSettings[chatId].TimeframeMinutes = timeframeMinutes;

                    #region OldAlertsDeleting
                    //v sluchae izmeneniya timeframe-a udalyaem stari token dobavlyaem novi
                    if (_oldAlertsToken.ContainsKey(chatId))
                    {
                        _oldAlertsToken[chatId].Cancel();
                        _oldAlertsToken.Remove(chatId);
                    }

                    //dobavlyaem token
                    CancellationTokenSource? cts = new CancellationTokenSource();
                    _oldAlertsToken[chatId] = cts;

                    //vizivaem fuknciyu v fonovom rejime
                    _ = ClearOldAlerts(timeframeMinutes, chatId, _dbService, cts.Token);
                    #endregion

                    string emoji = "⏱";
                    await client.SendMessage(chatId, $"{emoji} Пользователь ввёл таймфрейм: {timeframeMinutes} мин.");
                    _userSettings[chatId].Step++; // переходим к следующему шагу
                }
                else
                {
                    await client.SendMessage(chatId, "⚠️ Пожалуйста, введите число минут, например 15 или 60");
                }
            }

            // ---------- Шаг 4: запуск мониторинга ----------
            if (_userSettings[chatId].Step == 4 && update.Message != null)
            {
                int percent = _userSettings[chatId].PercentChange;
                int minutes = _userSettings[chatId].TimeframeMinutes;
                TimeSpan timeframe = TimeSpan.FromMinutes(minutes);


                _userSettings[chatId].PriceTracker = new PriceTracker(chatId, percent, timeframe,
                                            alert =>
                                            {
                                                _ = _dbService.SaveAlert(chatId, alert);
                                            },
                                            alert =>
                                            {
                                                _ = SendAlert(alert, _userSettings[chatId]);    // отправляем alert в Telegram
                                            }
                                            )
                {
                };

                _ = _dbService.CreateUserAlertsTable(chatId);

                foreach (var clients in Program.Clients) // Нужно сделать список клиентов общедоступным
                {
                    clients.OnPriceUpdate += async update =>
                    {
                        await _userSettings[chatId].PriceTracker.AddPriceUpdate(update, _dbService);
                    };
                }

                await client.SendMessage(chatId, "✅ Мониторинг запущен!");
                await client.SendMessage(chatId, "✏️ Если хотите изменить ваши вводные данные, напишите 'Change'.");
                _userSettings[chatId].Step = 5;
                return;
            }

        }

        public async Task SendAlert(Alert alert, MyUsers user)
        {

            string message = $"🚨 ALERT!\nБиржа: {alert.Exchange}\nПара: {alert.Pair}\nИзменение: {alert.ChangePercent:F2}% за {alert.Timeframe} мин.";
            await _client!.SendMessage(user.ChatId, message);

        }

        static async Task ClearOldAlerts(int timeframe, long chatId, DatabaseService dbService, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await dbService.ClearOldAlerts(chatId);

                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(timeframe), token);
                }
                catch (TaskCanceledException)
                {
                    break; // выйти из цикла, если таймер отменён
                }
            }
        }


    }
}

