using GPTipsBot.Api;
using GPTipsBot.Dtos;
using GPTipsBot.Services;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Message = Telegram.Bot.Types.Message;

namespace GPTipsBot
{
    public partial class TelegramBotWorker
    {
        private async Task<TelegramBotWorker?> HandleCommand(ITelegramBotClient botClient, TelegramGptMessage telegramGptMessage, CancellationToken cancellationToken)
        {
            var messageText = telegramGptMessage.Message;
            var chatId = telegramGptMessage.ChatId;
            
            if (messageText.StartsWith("/start"))
            {
                telegramGptMessage.Source = TelegramService.GetSource(messageText);
                userRepository.CreateUpdateUser(telegramGptMessage);
                await botClient.SendTextMessageAsync(chatId, BotResponse.Greeting, cancellationToken:cancellationToken);

                return null;
            }
            else if (messageText == "/help")
            {
                var desc = telegramBotApi.GetMyDescription();
                await botClient.SendTextMessageAsync(chatId, desc, cancellationToken:cancellationToken);

                return null;
            }

            return this;
        }

        private TelegramBotWorker? HandleRemove(Update update)
        {
            if (update.MyChatMember?.NewChatMember.Status == ChatMemberStatus.Kicked)
            {
                userRepository.SoftlyRemoveUser(update.MyChatMember.From.Id);

                return null;
            }

            return this;
        }

        private TelegramBotWorker HandleOnMaintenance(ITelegramBotClient botClient, string? messageText, long chatId, CancellationToken cancellationToken)
        {
            if (messageText == "/fix" && chatId == AppConfig.AdminId)
            {
                onMaintenance = !onMaintenance;
            }

            if (!onMaintenance)
            {
                return this;
            }

            botClient.SendTextMessageAsync(chatId, onMaintenance ? BotResponse.OnMaintenance : BotResponse.OnMaintenanceStop, cancellationToken: cancellationToken);

            return null;
        }

        private TelegramBotWorker? FilterMessage(Update update)
        {
            // Only process Message updates: https://core.telegram.org/bots/api#message
            if (update.Message is not { } message)
                return null;
            // Only process text messages
            if (message.Text is not { } messageText)
                return null;

            _logger.LogInformation($"Received a '{messageText}' message in chat {message.Chat.Id}.");

            return this;
        }

        private TelegramBotWorker? HandleGroupOrChannelMessage(Message message)
        {
            var isBotMentioned = message.Entities?.FirstOrDefault()?.Type == MessageEntityType.Mention && message.EntityValues.First().ToLower().Contains("gptip");
            var isGroupOrChannel = message.Chat.Type == ChatType.Group || message.Chat.Type == ChatType.Channel;

            if (!isBotMentioned && isGroupOrChannel)
            {
                return null;
            }

            if (isBotMentioned)
            {
                message.Text = message.Text.Substring(message.EntityValues.First().Length).Trim();
            }

            return this;
        }
        
        private TelegramBotWorker HandleRateLimiting(ITelegramBotClient botClient, long chatId, long telegramId, CancellationToken cancellationToken)
        {
            if (!MessageService.UserToMessageCount.TryGetValue(telegramId, out var existingValue))
            {
                MessageService.UserToMessageCount[telegramId] = (1, DateTime.UtcNow);
            }
            else
            {
                if (existingValue.messageCount + 1 > MessageService.MaxMessagesCountPerMinute)
                {
                    _logger.LogError("Max messages limit reached");
                    botClient.SendTextMessageAsync(chatId, BotResponse.TooManyRequests, cancellationToken: cancellationToken);

                    return null;
                }

                MessageService.UserToMessageCount[telegramId] = (existingValue.messageCount++, DateTime.UtcNow);
            }

            return this;
        }

        private async Task<(Message serviceMessage, Timer statusTimer)> BroadcastTypingStatus(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
        {
            System.Threading.Timer statusTimer = new((object o) =>
            {
                try
                {
                    botClient.SendChatActionAsync(chatId, ChatAction.Typing, cancellationToken);
                }
                catch (Exception ex) { _logger.LogInformation($"Error while SendChatActionAsync {ex.Message}"); }

            }, null, 0, 8 * 1000);

            var serviceMessage = await botClient.SendTextMessageAsync(chatId, BotResponse.Typing, cancellationToken: cancellationToken);

            return (serviceMessage, statusTimer);
        }

        private async Task StopStatusBroadcasting(ITelegramBotClient botClient, int messageId, long chatId, Timer statusTimer, CancellationToken cancellationToken)
        {
            await botClient.DeleteMessageAsync(chatId, messageId, cancellationToken: cancellationToken);
            statusTimer.Dispose();
        }

        private async Task<TelegramBotWorker> AskChatGpt(ITelegramBotClient botClient, TelegramGptMessage telegramGptMessage, CancellationToken cancellationToken)
        {
            var gtpResponse = await gptAPI.SendMessage(telegramGptMessage);
            telegramGptMessage.Reply = gtpResponse.text;
            messageRepository.AddBotResponse(telegramGptMessage);

            return this;
        }

        private async Task<TelegramBotWorker> SendChatGptReplyToUser(ITelegramBotClient botClient, TelegramGptMessage telegramGptMessage, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(telegramGptMessage.Reply))
            {
                throw new CustomException(BotResponse.SomethingWentWrong);
            }

            await botClient.SendTextMessageAsync(telegramGptMessage.ChatId, telegramGptMessage.Reply, cancellationToken: cancellationToken);
            return this;
        }
    }
}
