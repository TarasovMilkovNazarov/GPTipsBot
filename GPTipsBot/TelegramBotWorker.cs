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
            var sendViaTelegramBotClient = async (long chatId, string message, CancellationToken ct, IReplyMarkup markup) => 
                await botClient.SendTextMessageAsync(chatId, message, cancellationToken: ct, replyMarkup: markup);

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

            if (message.From == null)
            {
                throw new Exception();
            }

            var chatId = message.Chat.Id;

            if (message.Text.StartsWith("/start"))
            {
                var userDto = new CreateEditUser(message);
                userRepository.CreateUpdateUser(userDto);

                await sendViaTelegramBotClient(chatId, "Привет! Чем могу помочь?", cancellationToken, null);
                return;
            }
            else if (message.Text == "/help")
            {
                var desc = telegramBotApi.GetMyDescription();

                await sendViaTelegramBotClient(chatId, desc, cancellationToken, null);
                return;
            }

            _logger.LogInformation($"Received a '{messageText}' message in chat {chatId}.");
            await sendViaTelegramBotClient(chatId, BotResponse.Typing, cancellationToken, null);

            CancellationTokenSource source = new CancellationTokenSource();
            CancellationToken waitResponseCancellationToken = source.Token;
            var sendChatActionTasks = new List<Task>();
            Timer timer = new((object o) =>
            {
                botClient.SendChatActionAsync(chatId, ChatAction.Typing, waitResponseCancellationToken);
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
                    await sendViaTelegramBotClient(chatId, BotResponse.TooManyRequests, cancellationToken, null);

                    return;
                }

                MessageService.UserToMessageCount[message.From.Id] = (existingValue.messageCount++, DateTime.UtcNow);
            }

            var sendMessage = await gptAPI.SendMessage(messageText);
            source.Cancel();
            timer.Dispose();
            
            if (sendMessage.isSuccessful)
            {
                await sendViaTelegramBotClient(chatId, sendMessage.response, cancellationToken, null);

                return;
            }

            await sendViaTelegramBotClient(chatId, BotResponse.SomethingWentWrong, cancellationToken, null);
        }

        Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            var ErrorMessage = exception switch
            {
                ApiRequestException apiRequestException
                    => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => exception.ToString()
            };

            _logger.LogError(ErrorMessage);

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
