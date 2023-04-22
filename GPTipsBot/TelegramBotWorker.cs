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
using Npgsql;
using Dapper;
using GPTipsBot.Repositories;
using GPTipsBot.Dtos;

namespace GPTipsBot
{
    public class TelegramBotWorker : IHostedService, IDisposable
    {
        private readonly ILogger<TelegramBotWorker> _logger;
        private readonly IUserRepository userRepository;
        private OpenAIService openAiService;
        private readonly string openaiBaseUrl = "https://api.openai.com/v1/engines/davinci-codex";

        public TelegramBotWorker(ILogger<TelegramBotWorker> logger, IUserRepository userRepository)
        {
            _logger = logger;
            this.userRepository = userRepository;
            openAiService = new OpenAIService(new OpenAiOptions()
            {
                ApiKey = AppConfig.OpenAiToken
            });
            openAiService.SetDefaultModelId(GptModels.Models.Davinci);
        }

        async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            // Only process Message updates: https://core.telegram.org/bots/api#message
            if (update.Message is not { } message)
                return;
            // Only process text messages
            if (message.Text is not { } messageText)
                return;

            var chatId = message.Chat.Id;

            Console.WriteLine($"Received a '{messageText}' message in chat {chatId}.");
            

            try
            {
                if (message.From == null)
                {
                    throw new Exception();
                }

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
            catch (Exception)
            {
                _logger.LogError("Can't define user telegramId for message");
            }
            

            var completionResult = await openAiService.ChatCompletion.CreateCompletion(new ChatCompletionCreateRequest
            {
                Messages = new List<ChatMessage>
                {
                    ChatMessage.FromUser(messageText),
                },
                Model = GptModels.Models.ChatGpt3_5Turbo,
                MaxTokens = 1//optional
            });
            if (completionResult.Successful)
            {
                Console.WriteLine(completionResult.Choices.First().Message.Content);
                // Echo received message text
                Message sentMessage = await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: completionResult.Choices.First().Message.Content,
                    cancellationToken: cancellationToken);
            }
        }

        Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            var ErrorMessage = exception switch
            {
                ApiRequestException apiRequestException
                    => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => exception.ToString()
            };

            Console.WriteLine(ErrorMessage);
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
            Console.WriteLine($"Start listening for @{me.Username}");
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
