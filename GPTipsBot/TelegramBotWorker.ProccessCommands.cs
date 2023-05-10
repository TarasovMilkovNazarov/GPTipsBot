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
        public async Task<bool> ProccessCommand(ITelegramBotClient botClient, TelegramGptMessage telegramGptMessage, 
            string? messageText, long chatId, CancellationToken cancellationToken)
        {
            var isCommand = false;
            
            if (messageText.StartsWith("/start"))
            {
                telegramGptMessage.Source = TelegramService.GetSource(messageText);
                userRepository.CreateUpdateUser(telegramGptMessage);
                await botClient.SendTextMessageAsync(chatId, BotResponse.Greeting, cancellationToken:cancellationToken);
                isCommand = true;
            }
            else if (messageText == "/help")
            {
                var desc = telegramBotApi.GetMyDescription();

                await botClient.SendTextMessageAsync(chatId, desc, cancellationToken:cancellationToken);
                isCommand = true;
            }
            else if (messageText == "/fix" && chatId == AppConfig.AdminId)
            {
                onMaintenance = !onMaintenance;
            }
            //else if (messageText == "/resetContext")
            //{
            //    messageRepository.ResetContext(telegramGptMessage, chatId);

            //    await botClient.SendTextMessageAsync(chatId, BotResponse.ContextUpdated, cancellationToken:cancellationToken);
            //    return;
            //}

            if (isCommand)
            {
                messageRepository.AddUserMessage(telegramGptMessage);
            }

            return isCommand;
        }
    }
}
