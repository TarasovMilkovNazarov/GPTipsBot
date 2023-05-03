using Telegram.Bot.Exceptions;
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

namespace GPTipsBot
{
    public class TelegramBotWorker : IHostedService, IDisposable
    {
        private readonly ILogger<TelegramBotWorker> _logger;
        private readonly IUserRepository userRepository;
        private readonly GptAPI gptAPI;
        private readonly TelegramBotAPI telegramBotApi;
        private bool onMaintenance = true;

        public TelegramBotWorker(ILogger<TelegramBotWorker> logger, IUserRepository userRepository, 
            GptAPI gptAPI, TelegramBotAPI telegramBotApi)
        {
            _logger = logger;
            this.userRepository = userRepository;
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

            if (onMaintenance)
            {
                await botClient.SendTextMessageAsync(chatId, BotResponse.OnMaintenance, cancellationToken:cancellationToken);
                return;
            }

            if (message.Text.StartsWith("/start"))
            {
                var userDto = new CreateEditUser(message);
                userRepository.CreateUpdateUser(userDto);
                await botClient.SendTextMessageAsync(chatId, BotResponse.Greeting, cancellationToken:cancellationToken);
                return;
            }
            else if (message.Text == "/help")
            {
                var desc = telegramBotApi.GetMyDescription();

                await botClient.SendTextMessageAsync(chatId, desc, cancellationToken:cancellationToken);
                return;
            }
            
            try
            {
                await botClient.SendTextMessageAsync(chatId, BotResponse.Typing, cancellationToken:cancellationToken);
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

            var sendMessage = await gptAPI.SendMessage(messageText);
            timer.Dispose();
            
            if (sendMessage.isSuccessful)
            {
                try
                {
                    await botClient.SendTextMessageAsync(chatId, sendMessage.response, cancellationToken: cancellationToken);
                }
                catch (Exception ex) {
                    telegramBotApi.LogErrorMessageFromApiResponse(ex);
                    await botClient.SendTextMessageAsync(chatId, BotResponse.SomethingWentWrong, cancellationToken: cancellationToken);
                }
            }
        }

        Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            telegramBotApi.LogErrorMessageFromApiResponse(exception);

            return Task.CompletedTask;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var botClient = new TelegramBotClient(AppConfig.TelegramToken);

            // StartReceiving does not block the caller thread. Receiving is done on the ThreadPool.
            ReceiverOptions receiverOptions = new()
            {
                AllowedUpdates = Array.Empty<UpdateType>() // receive all update types
            };

            botClient.StartReceiving(
                updateHandler: HandleUpdateAsync,
                pollingErrorHandler: HandlePollingErrorAsync,
                receiverOptions: receiverOptions
            );

            await botClient.SetMyCommandsAsync(new List<BotCommand>() { 
                new BotCommand { Command = "/start", Description = "Начать пользоваться ботом" },
                new BotCommand { Command = "/help", Description = "Инструкция по применению" },
            });

            var me = await botClient.GetMeAsync();
            _logger.LogInformation($"Start listening for @{me.Username}");
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Telegram background service is stopping.");

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _logger.LogInformation("Telegram bs disposing");
        }
    }
}
