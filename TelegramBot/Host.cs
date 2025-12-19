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

namespace ScreenerTest.TelegramBot
{
    class Host
    {
        // key = chatId, value = UserSettings
        private Dictionary<long, MyUsers> _userSettings = new Dictionary<long, MyUsers>();

        private static string token { get; set; } = "8525002273:AAGGq1e1j1kRvgU76tEubAAEtiW_fK_gXHE";
        public static TelegramBotClient? client;
        int step = 0;

        public Host()
        {
            client = new TelegramBotClient(token);
        }

        public void Start()
        {
            Console.WriteLine("Bot is ran");
            client.StartReceiving(UpdateHandler, ErrorHandler);
        }
        private async Task ErrorHandler(ITelegramBotClient client, Exception exception, HandleErrorSource source, CancellationToken token)
        {
            Console.WriteLine($"Error: {exception.Message}");
            await Task.CompletedTask;
        }


        private async Task UpdateHandler(ITelegramBotClient client, Update update, CancellationToken token)
        {

            // ---------- Шаг 0: старт команды ----------
            if (update.Message != null && step == 0)
            {
                long chatId = update.Message.Chat.Id;
                string text = update.Message.Text ?? "";

                if (text == "/start")
                {
                    var keyboard = new InlineKeyboardMarkup(new[]
                    {
                        new[] { InlineKeyboardButton.WithCallbackData("Начать мониторинг", "start_monitoring") },
                        new[] { InlineKeyboardButton.WithCallbackData("Помощь", "help") }
                    });

                    await client.SendMessage(chatId, "🤖 Бот готов! Выберите действие:", replyMarkup: keyboard);
                    step++;
                    return;
                }
            }

            // ---------- Шаг 1: обработка нажатия кнопок ----------
            if (update.CallbackQuery is { } query && step == 1)
            {
                if (query.Data == "start_monitoring")
                {
                    await client.SendMessage(query.Message.Chat.Id, "✏️ Введите процент изменения:");
                    step++;
                    return;
                }

                if (query.Data == "help")
                {
                    await client.SendMessage(query.Message.Chat.Id, "ℹ️ Этот бот отслеживает сигналы и уведомляет вас о процентных изменениях.");
                    step += 2; // пропускаем шаг ввода процента
                    return;
                }
            }

            // ---------- Шаг "Change" ----------
            if (update.Message?.Text == "Change")
            {
                step = 2;
            }

            // ---------- Шаг 2: ввод процента ----------
            if (step == 2 && update.Message != null)
            {
                long chatId = update.Message.Chat.Id;
                string text = update.Message.Text ?? "";

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
                    step++;
                }
                else
                {
                    await client.SendMessage(chatId, "⚠️ Пожалуйста, введите число в формате процента, например 12");
                }
                return;
            }

            // ---------- Шаг 3: ввод таймфрейма ----------
            if (step == 3 && update.Message != null)
            {
                long chatId = update.Message.Chat.Id;
                string text = update.Message.Text?.Trim() ?? "";

                if (int.TryParse(text.Replace("м", ""), out int timeframeMinutes) && timeframeMinutes > 0)
                {
                    _userSettings[chatId].TimeframeMinutes = timeframeMinutes;

                    string emoji = "⏱";
                    await client.SendMessage(chatId, $"{emoji} Пользователь ввёл таймфрейм: {timeframeMinutes} мин.");
                    step++; // переходим к следующему шагу
                }
                else
                {
                    await client.SendMessage(chatId, "⚠️ Пожалуйста, введите число минут, например 15 или 60");
                }
            }

            // ---------- Шаг 4: запуск мониторинга ----------
            if (step == 4 && update.Message != null)
            {
                long chatId = update.Message.Chat.Id;
                int percent = _userSettings[chatId].PercentChange;
                int minutes = _userSettings[chatId].TimeframeMinutes;
                TimeSpan timeframe = TimeSpan.FromMinutes(minutes);


                _userSettings[chatId].PriceTracker = new PriceTracker(percent, timeframe,
                                            alert =>
                                            {
                                                //dbService.SaveAlert(alert);
                                            },
                                            alert =>
                                            {
                                                _ = SendAlert(alert, _userSettings[chatId]);    // отправляем alert в Telegram
                                            }
                                            )
                {
                };

                await client.SendMessage(chatId, "✅ Мониторинг запущен!");
                await client.SendMessage(chatId, "✏️ Если хотите изменить ваши вводные данные, напишите 'Change'.");
                step = 5;
                return;
            }
        }

        public async Task SendAlert(Alert alert,MyUsers user)
        {
            // Здесь пробегаем по всем пользователям и отправляем alert

                string message = $"🚨 ALERT!\nБиржа: {alert.Exchange}\nПара: {alert.Pair}\nИзменение: {alert.ChangePercent:F2}% за {alert.Timeframe} мин.";
                await client!.SendMessage(user.ChatId, message);

        }


    }
}

