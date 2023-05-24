using GPTipsBot.Api;
using GPTipsBot.Services;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace GPTipsBot.Extensions
{
    public static class BotClientExtensions
    {
        public static async Task SendTextMessageWithMenuKeyboard(this ITelegramBotClient botClient, long chatId, string text, CancellationToken cancellationToken)
        {
            await botClient.SendTextMessageAsync(chatId, text, cancellationToken: cancellationToken, replyMarkup: TelegramBotUIService.startKeyboard);
        }
    }
}
