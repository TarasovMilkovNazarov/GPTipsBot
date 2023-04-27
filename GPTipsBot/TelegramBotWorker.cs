using GptModels = OpenAI.GPT3.ObjectModels;
using OpenAI.GPT3.ObjectModels.RequestModels;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot;
using OpenAI.GPT3.Managers;
using OpenAI.GPT3;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using GPTipsBot.Repositories;
using GPTipsBot.Dtos;
using GPTipsBot.Services;

namespace GPTipsBot
{
    public class TelegramBotWorker : IHostedService, IDisposable
    {
        private readonly ILogger<TelegramBotWorker> _logger;
        private readonly IUserRepository userRepository;
        private readonly GptAPI gptAPI;

        public TelegramBotWorker(ILogger<TelegramBotWorker> logger, IUserRepository userRepository, GptAPI gptAPI)
        {
            _logger = logger;
            this.userRepository = userRepository;
            this.gptAPI = gptAPI;
        }

        async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            var sendViaTelegramBotClient = async (long chatId, string message, CancellationToken ct) => 
                await botClient.SendTextMessageAsync(chatId, message, cancellationToken: ct);

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
            await sendViaTelegramBotClient(chatId, "Обработка запросов займет время, но я буду ждать ответа сервера и сообщу вам о результатах. Благодарю за терпение!", cancellationToken);

            CancellationTokenSource source = new CancellationTokenSource();
            CancellationToken waitResponseCancellationToken = source.Token;
            var sendChatActionTasks = new List<Task>();
            Timer timer = new Timer((Object o) =>
            {
                botClient.SendChatActionAsync(chatId, ChatAction.Typing, waitResponseCancellationToken);
            }, null, 0, 7 * 1000);

            if (message.From == null)
            {
                throw new Exception();
            }

            try
            {
                var userDto = new CreateEditUser()
                {
                    Id = message.From.Id,
                    TelegramId = message.From.Id,
                    FirstName = message.From.FirstName,
                    LastName = message.From.LastName,
                    Message = message.Text,
                    TimeStamp = DateTime.UtcNow
                };
                userRepository.CreateUser(userDto);
            }
            catch (Exception ex)
            {
                _logger.LogError("Can't create user ", ex.Message);
            }

            if (!MessageService.UserToMessageCount.TryGetValue(message.From.Id, out var existingValue))
            {
                MessageService.UserToMessageCount[message.From.Id] = (1, DateTime.UtcNow);
            }
            else
            {
                if (existingValue.messageCount + 1 > MessageService.MaxMessagesCountPerMinute)
                {
                    _logger.LogError("Max messages limit reached");
                    await sendViaTelegramBotClient(chatId, "Слишком много запросов, повторите отправку через 1 минуту", cancellationToken);

                    return;
                }

                MessageService.UserToMessageCount[message.From.Id] = (existingValue.messageCount++, DateTime.UtcNow);
            }

            var sendMessage = await gptAPI.SendMessage(messageText);
            source.Cancel();
            timer.Dispose();
            
            if (sendMessage.isSuccessful)
            {
                await sendViaTelegramBotClient(chatId, sendMessage.response, cancellationToken);

                return;
            }

            await sendViaTelegramBotClient(chatId, "Что-то пошло не так, попробуйте ещё раз", cancellationToken);
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
