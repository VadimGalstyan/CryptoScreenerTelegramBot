using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types.Enums;
using CryptoScreener.Telegram;
using System.Collections.Concurrent;

namespace CryptoScreener.TelegramBot
{
    public class TelegramHost
    {
        public static TelegramBotClient? _client;
        private string _token = "";

        public ConcurrentDictionary<long, MyUsers> _userSettings = new();

        public TelegramHost()
        {
            _client = new TelegramBotClient(_token);
        }

        public void Start()
        {
            _client.StartReceiving(HandleUpdateAsync, HandleErrorAsync);
            Console.WriteLine($"[System] Bot started: {DateTime.Now}");
        }

        public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken ct)
        {

            //if (update.Type != UpdateType.Message || update.Message?.Text == null) return;

            //long chatId = update.Message.Chat.Id;
            //string text = update.Message.Text;

            //if (!_userSettings.ContainsKey(chatId))
            //{
            //    _userSettings[chatId] = new MyUsers((int)chatId);

            //    await _client.SendMessage(chatId,
            //        "👋 Hi! I am a screener bot for tracking price and volume on exchanges.\n\n" +
            //        "🚀 To get started and configure filters, press /start");
            //    return;
            //}

            //var user = _userSettings[chatId];

            // 1. If the bot is waiting for numeric input (Step > 0)

            if (update.Message == null || update.Message.Text == null) return;

            long chatId = update.Message.Chat.Id;
            string text = update.Message.Text;

            // 1. If this is the /start command — initialize the user
            if (text == "/start" && !_userSettings.ContainsKey(chatId))
            {
                if (!_userSettings.ContainsKey(chatId))
                {
                    _userSettings[chatId] = new MyUsers((int)chatId);
                }
                await ShowMainMenu(chatId, "Select the monitoring tool you need:");
                return;
            }

            // 2. If the user is not in the dictionary (did not press /start), ignore them
            if (!_userSettings.ContainsKey(chatId))
            {
                await _client.SendMessage(chatId, "👋 Hi! I am a screener bot for tracking price and volume on exchanges.\n\n" +
                    "⚠️ Please enter /start to begin working with the bot.");
                return;
            }

            // 3. Continue with the normal logic (switch-case, percent input, etc.)
            var user = _userSettings[chatId];

            if (user.Step > 0)
            {
                await HandleNumericInput(chatId, user, text);
                return;
            }

            // 2. Main navigation via buttons
            switch (text)
            {
                case "🏠 Main Menu":
                    user.Step = 0;
                    await ShowMainMenu(chatId, "Select the monitoring tool you need:");
                    break;

                case "📉 Price Screener":
                    user.SelectedMode = "price";
                    await ShowExchanges(chatId);
                    break;

                case "📊 Volume Screener":
                    user.SelectedMode = "volume";
                    await ShowExchanges(chatId);
                    break;

                case "Binance":
                case "ByBit":
                case "Bitget":
                case "Mexc":
                case "Gate":
                    user.SelectedExchange = text.ToLower();
                    await ShowMarketType(chatId, user.SelectedExchange);
                    break;

                case "📈 Spot":
                case "🚀 Futures":
                    user.SelectedMarket = text.Contains("Spot") ? "spot" : "futures";
                    await ShowActionMenu(chatId);
                    break;

                case "🟢 Enable":
                    string keyOn = user.GetKey();
                    if (user.SelectedMode == "price")
                    {
                        Console.WriteLine("price");
                        user.TurnOnPriceScreener(keyOn, user.TempPercent, user.TempTimeframe);
                    }
                    else
                        user.TurnOnVolumeScreener(keyOn, user.TempPercent, user.TempTimeframe);

                    await _client.SendMessage(chatId, "✅ Monitoring successfully started!");
                    await ShowActionMenu(chatId);
                    break;

                case "🔴 Disable":
                    string keyOff = user.GetKey();
                    if (user.SelectedMode == "price")
                    {
                        if (user._priceScreener.TryGetValue(keyOff, out var pScreener) && pScreener != null)
                        {
                            pScreener.Dispose();
                            user._priceScreener[keyOff] = null;
                        }
                    }
                    else if (user.SelectedMode == "volume")
                    {
                        //if (user._volumeScreener.TryGetValue(keyOff, out var vScreener) && vScreener != null)
                        //{
                        //    vScreener.Dispose(); // Don't forget to add IDisposable to VolumeScreener as well
                        //    user._volumeScreener[keyOff] = null;
                        //}
                    }

                    await _client.SendMessage(chatId, "❌ <b>Monitoring stopped.</b>\nAll background tasks have been terminated.", parseMode: ParseMode.Html);

                    await ShowActionMenu(chatId);
                    break;

                case "🔢 Change %":
                    user.Step = 1;
                    await _client.SendMessage(chatId, "Enter the desired percentage (numbers only):", replyMarkup: new ReplyKeyboardRemove());
                    break;

                case "🕒 Change Timeframe":
                    user.Step = 2;
                    await _client.SendMessage(chatId, "Enter the timeframe in minutes:", replyMarkup: new ReplyKeyboardRemove());
                    break;

                case "ℹ️ Help":
                    string helpText = $@"
    🛠 <b>Help & Guide</b>

<b>🤖 What is this bot for?</b>
The bot monitors hundreds of trading pairs on exchanges in real time to find:
1. <b>Pumps and Dumps:</b> Sharp price changes over a selected period.
2. <b>Abnormal Volumes:</b> Spikes in trading activity (comparing the current 5-minute candle with the previous one).

<b>⚙️ How to configure?</b>
1. Press <b>«⚙️ Configure Exchanges»</b>.
2. Select the desired screener (price or volume).
3. Select an exchange and market (Spot/Futures).
4. Set parameters using the <b>«🔢 Change %»</b> button:
- For price: % deviation (e.g., 2).
- For volume: growth multiplier (e.g., 2.5 — means 2.5x growth).
5. Press <b>«🟢 Enable»</b>.

<b>💡 Tip:</b> Do not set values too low to avoid market noise.

    --- 
<b>💬 Feedback & Support:</b>
If you have suggestions or found an issue, write to us in the channel:
👉 <a href='https://t.me/vash_kanal'>Subscribe to channel</a>";

                    await _client.SendMessage(chatId, helpText, parseMode: ParseMode.Html);
                    break;
            }
        }

        private async Task HandleNumericInput(long chatId, MyUsers user, string text)
        {
            //if (short.TryParse(text, out short val) && val > 0)
            //{
            //    string key = user.GetKey();

            //    if (user.Step == 1) // Change percent
            //    {
            //        user.TempPercent = val;
            //        // If the screener is already created (enabled), just update its field
            //        if (user._priceScreener[key] != null)
            //            user._priceScreener[key]._percent = val;
            //    }
            //    else if (user.Step == 2) // Change timeframe
            //    {
            //        user.TempTimeframe = val;
            //        // If the screener is already created (enabled), just update its field
            //        if (user._priceScreener[key] != null)
            //            user._priceScreener[key]._timeframe = val;
            //    }

            //    user.Step = 0;
            //    await _client.SendMessage(chatId, $"✅ Value {val} successfully updated in real time!");
            //    await ShowActionMenu(chatId);
            //}
            if (short.TryParse(text, out short val))
            {
                string key = user.GetKey();

                if (user.Step == 1) // Configure percent (shared for all)
                {
                    if (val <= 0)
                    {
                        await _client.SendMessage(chatId, "⚠️ Percentage must be greater than 0.");
                        return;
                    }

                    user.TempPercent = val;

                    // Update PriceScreener if it is active
                    if (user.SelectedMode == "price" && user._priceScreener.TryGetValue(key, out var pScreener) && pScreener != null)
                        pScreener.SetPercent(val);

                    // Update VolumeScreener (if it has a _percent field)

                    if (user.SelectedMode == "volume" && user._volumeScreener.TryGetValue(key, out var vScreener) && vScreener != null)
                        vScreener.SetPercent(val);

                    await _client.SendMessage(chatId, $"✅ Percentage {val}% successfully set!");
                }
                else if (user.Step == 2) // Configure timeframe
                {
                    // CHECK: If we are in PRICE mode, limit to 60 minutes
                    if (user.SelectedMode == "price")
                    {
                        if (val < 1 || val > 60)
                        {
                            await _client.SendMessage(chatId, "⚠️ <b>Error:</b> For the price screener, the timeframe must be between 1 and 60 minutes.", parseMode: ParseMode.Html);
                            return;
                        }

                        user.TempTimeframe = val;

                        if (user._priceScreener.TryGetValue(key, out var pScreener) && pScreener != null)
                            pScreener._timeframe = val;

                        await _client.SendMessage(chatId, $"✅ Price timeframe set to {val} min.");
                    }
                    else
                    {
                        // If this is VOLUME mode and there is no timeframe there, either throw an error
                        // or allow input without the 60-minute limit if it is still needed there for some reason.
                        await _client.SendMessage(chatId, "ℹ️ In Volume mode, the timeframe is not used or is configured differently.");
                        return;
                    }
                }

                user.Step = 0;
                await ShowActionMenu(chatId);
            }
            else
            {
                await _client.SendMessage(chatId, "❌ Error! Please enter a positive whole number.");
            }
        }

        // --- MENU RENDERING METHODS ---

        private async Task ShowMainMenu(long chatId, string msg)
        {
            var rk = new ReplyKeyboardMarkup(new[] {
                new KeyboardButton[] { "📉 Price Screener", "📊 Volume Screener" },
                new KeyboardButton[] { "ℹ️ Help" }
            })
            { ResizeKeyboard = true };
            await _client.SendMessage(chatId, msg, replyMarkup: rk);
        }

        private async Task ShowExchanges(long chatId)
        {
            var rk = new ReplyKeyboardMarkup(new[] {
                new KeyboardButton[] { "Binance", "ByBit", "Bitget" },
                new KeyboardButton[] { "Mexc", "Gate" },
                new KeyboardButton[] { "🏠 Main Menu" }
            })
            { ResizeKeyboard = true };
            await _client.SendMessage(chatId, "Select the exchange you are interested in:", replyMarkup: rk);
        }

        private async Task ShowMarketType(long chatId, string exchange)
        {
            var rk = new ReplyKeyboardMarkup(new[] {
                new KeyboardButton[] { "📈 Spot", "🚀 Futures" },
                new KeyboardButton[] { "🏠 Main Menu" }
            })
            { ResizeKeyboard = true };
            await _client.SendMessage(chatId, $"Exchange selected: {exchange}. Now choose the market type:", replyMarkup: rk);
        }

        private async Task ShowActionMenu(long chatId)
        {
            var user = _userSettings[chatId];
            string key = user.GetKey();

            // Check activity via the dictionaries in MyUsers
            bool isActive = user.SelectedMode == "price"
                ? user._priceScreener.ContainsKey(key) && user._priceScreener[key] != null
                : user._volumeScreener.ContainsKey(key) && user._volumeScreener[key] != null;

            var rk = new ReplyKeyboardMarkup(new[] {
                new KeyboardButton[] { isActive ? "🔴 Disable" : "🟢 Enable" },
                new KeyboardButton[] { "🔢 Change %", "🕒 Change Timeframe" },
                new KeyboardButton[] { "🏠 Main Menu" }
            })
            { ResizeKeyboard = true };

            string statusText = isActive ? "✅ RUNNING" : "❌ STOPPED";

            //string info = $"🛠 <b>INFORMATION</b>\n" +
            //              $"--------------------------\n" +
            //              $"Exchange: {user.SelectedExchange.ToUpper()} ({user.SelectedMarket})\n" +
            //              $"Mode: {user.SelectedMode}\n" +
            //              $"Status: {statusText}\n" +
            //              $"Settings: {user.TempPercent}% / {user.TempTimeframe} min.";
            // 1. Determine which values to display

            short displayPercent = user.TempPercent;
            short displayTimeframe = user.TempTimeframe;

            // 2. If the screener is active, take the real settings directly from it
            if (user.SelectedMode == "price" && user._priceScreener.TryGetValue(key, out var pScreener) && pScreener != null)
            {
                displayPercent = pScreener._percent;
                displayTimeframe = pScreener._timeframe;
            }
            else if (user.SelectedMode == "volume" && user._volumeScreener.TryGetValue(key, out var vScreener) && vScreener != null)
            {
                // Same for Volume, if those fields exist there
                // displayPercent = vScreener._percent; 
            }
            string info = $"🛠 <b>INFORMATION</b>\n" +
                          $"--------------------------\n" +
                          $"Exchange: {user.SelectedExchange.ToUpper()} ({user.SelectedMarket})\n" +
                          $"Mode: {user.SelectedMode}\n" +
                          $"Status: {statusText}\n" +
                          $"Settings: {displayPercent}% / {displayTimeframe} min.";

            await _client.SendMessage(chatId, info, replyMarkup: rk, parseMode: ParseMode.Html);
        }

        private Task HandleErrorAsync(ITelegramBotClient c, Exception e, CancellationToken ct)
        {
            Console.WriteLine("[Error] " + e.Message);
            return Task.CompletedTask;
        }

        public static async Task SendAlert(long chatId, string message)
        {
            try
            {
                if (_client != null)
                {
                    await _client.SendMessage(
                        chatId: chatId,
                        text: message,
                        parseMode: ParseMode.Html // Allows the use of <b></b> and other tags
                    );
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error Sending Alert] {ex.Message}");
            }
        }
    }
}