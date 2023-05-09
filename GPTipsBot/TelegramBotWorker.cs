using Telegram.Bot.Types;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using GPTipsBot.Repositories;
using GPTipsBot.Dtos;
using GPTipsBot.Services;
using GPTipsBot.Api;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Exceptions;

namespace GPTipsBot
{
    public partial class TelegramBotWorker : IHostedService, IDisposable
    {
        private readonly ILogger<TelegramBotWorker> _logger;
        private readonly UserRepository userRepository;
        private readonly MessageContextRepository messageRepository;
        private readonly GptAPI gptAPI;
        private readonly TelegramBotAPI telegramBotApi;
        private bool onMaintenance = false;

        public TelegramBotWorker(ILogger<TelegramBotWorker> logger, UserRepository userRepository, 
            MessageContextRepository messageRepository, GptAPI gptAPI, TelegramBotAPI telegramBotApi)
        {
            _logger = logger;
            this.userRepository = userRepository;
            this.messageRepository = messageRepository;
            this.gptAPI = gptAPI;
            this.telegramBotApi = telegramBotApi;
        }

        async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            if (update.MyChatMember?.NewChatMember.Status == ChatMemberStatus.Kicked)
            {
                userRepository.SoftlyRemoveUser(update.MyChatMember.From.Id);
                return;
            }

            // Only process Message updates: https://core.telegram.org/bots/api#message
            if (update.Message is not { } message)
                return;
            // Only process text messages
            if (message.Text is not { } messageText)
                return;

            var chatId = message.Chat.Id;
            _logger.LogInformation($"Received a '{messageText}' message in chat {chatId}.");

            var telegramGptMessage = new TelegramGptMessage(message);
            await ProccessCommand(botClient, telegramGptMessage, messageText, chatId, cancellationToken);
            Message serviceMessage = null;

            try
            {
                userRepository.CreateUpdateUser(telegramGptMessage);
                messageRepository.AddUserMessage(telegramGptMessage);
                serviceMessage = await botClient.SendTextMessageAsync(chatId, BotResponse.Typing, cancellationToken:cancellationToken);
            }
            catch (Exception ex)
            {
                telegramBotApi.LogErrorMessageFromApiResponse(ex);
            }

            var sendChatActionTasks = new List<Task>();
            Timer timer = new((object o) =>
            {
                try
                {
                    botClient.SendChatActionAsync(chatId, ChatAction.Typing, cancellationToken);
                }
                catch (Exception ex) { _logger.LogInformation($"Error while SendChatActionAsync {ex.Message}"); }
                
            }, null, 0, 8 * 1000);

            if (!MessageService.UserToMessageCount.TryGetValue(message.From.Id, out var existingValue))
            {
                MessageService.UserToMessageCount[message.From.Id] = (1, DateTime.UtcNow);
            }
            else
            {
                if (existingValue.messageCount + 1 > MessageService.MaxMessagesCountPerMinute)
                {
                    _logger.LogError("Max messages limit reached");
                    try
                    {
                        await botClient.SendTextMessageAsync(chatId, BotResponse.TooManyRequests, cancellationToken: cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        telegramBotApi.LogErrorMessageFromApiResponse(ex);
                    }

                    return;
                }

                MessageService.UserToMessageCount[message.From.Id] = (existingValue.messageCount++, DateTime.UtcNow);
            }

            (bool isSuccessful, string? text) gtpResponse = (isSuccessful: false, text: null);
            try
            {
                gtpResponse = await gptAPI.SendMessage(telegramGptMessage);
                telegramGptMessage.Reply = gtpResponse.text;
                messageRepository.AddBotResponse(telegramGptMessage);
            }
            catch (Exception ex)
            {
                string? error = null;
                if (ex is CustomException)
                {
                    error = ex.Message;
                }

                timer.Dispose();
                if (serviceMessage != null)
                {
                    await botClient.DeleteMessageAsync(chatId, serviceMessage.MessageId, cancellationToken: cancellationToken);
                }

                await botClient.SendTextMessageAsync(chatId, error ?? BotResponse.SomethingWentWrong, cancellationToken: cancellationToken);

                return;
            }

            timer.Dispose();
            
            if (gtpResponse.isSuccessful)
            {
                try
                {
                    if (serviceMessage != null)
                    {
                        await botClient.DeleteMessageAsync(chatId, serviceMessage.MessageId, cancellationToken: cancellationToken);
                    }
                    
                    await botClient.SendTextMessageAsync(chatId, gtpResponse.text, cancellationToken: cancellationToken);

                    return;
                }
                catch (Exception ex) {
                    telegramBotApi.LogErrorMessageFromApiResponse(ex);
                }
            }

            await botClient.SendTextMessageAsync(chatId, BotResponse.SomethingWentWrong, cancellationToken: cancellationToken);
        }
    }
}
