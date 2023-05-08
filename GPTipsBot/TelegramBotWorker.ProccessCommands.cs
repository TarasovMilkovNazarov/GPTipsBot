using GPTipsBot.Api;
using GPTipsBot.Dtos;
using GPTipsBot.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace GPTipsBot
{
    public partial class TelegramBotWorker
    {
        public async Task ProccessCommand(ITelegramBotClient botClient, TelegramGptMessage telegramGptMessage, 
            string? messageText, long chatId, CancellationToken cancellationToken)
        {
            if (messageText == "maintenance" && chatId == AppConfig.AdminId)
            {
                onMaintenance = !onMaintenance;
            }
            if (onMaintenance)
            {
                await botClient.SendTextMessageAsync(chatId, BotResponse.OnMaintenance, cancellationToken:cancellationToken);
                return;
            }

            if (messageText.StartsWith("/start"))
            {
                telegramGptMessage.Source = TelegramService.GetSource(messageText);
                userRepository.CreateUpdateUser(telegramGptMessage);
                await botClient.SendTextMessageAsync(chatId, BotResponse.Greeting, cancellationToken:cancellationToken);
                return;
            }
            else if (messageText == "/help")
            {
                var desc = telegramBotApi.GetMyDescription();

                await botClient.SendTextMessageAsync(chatId, desc, cancellationToken:cancellationToken);
                return;
            }
            //else if (messageText == "/resetContext")
            //{
            //    messageRepository.ResetContext(telegramGptMessage, chatId);

            //    await botClient.SendTextMessageAsync(chatId, BotResponse.ContextUpdated, cancellationToken:cancellationToken);
            //    return;
            //}
        }
    }
}
