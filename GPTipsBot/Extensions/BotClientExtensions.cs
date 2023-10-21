using GPTipsBot.Services;
using Telegram.Bot;

namespace GPTipsBot.Extensions
{
    public static class BotClientExtensions
    {
        public static async Task SendTextMessageWithMenuKeyboard(this ITelegramBotClient botClient, long chatId, string text)
        {
            await botClient.SendTextMessageAsync(chatId, text, replyMarkup: TelegramBotUIService.startKeyboard);
        }
    }
}
