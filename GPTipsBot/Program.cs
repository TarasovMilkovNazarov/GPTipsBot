using OpenAI.GPT3.Managers;
using OpenAI.GPT3;
using RestSharp;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using OpenAI.GPT3.ObjectModels;
using OpenAI.GPT3.ObjectModels.RequestModels;
using System.Threading;
using Newtonsoft.Json.Linq;
using System.Net;
using GPTipsBot;

const string telegramToken = "6272630353:AAG6zDC3BTBQ0dt09nHE6_mN4RpDRUEjPDM";
const long chatId = 486363646;
const string openaiBaseUrl = "https://api.openai.com/v1/engines/davinci-codex";

var openAiHttpClient = new RestClient(openaiBaseUrl);

// Define the message to send to the GPT chat
var message = "Hello, GPT!";

var openAiService = new OpenAIService(new OpenAiOptions()
{
    //ApiKey =  Environment.GetEnvironmentVariable("MY_OPEN_AI_API_KEY"),
    ApiKey = "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"
});
openAiService.SetDefaultModelId(Models.Davinci);

var tgBotApi = new TelegramBotAPI(telegramToken, chatId);
tgBotApi.SetMyDescription("DescriptionExample");

var botClient = new TelegramBotClient(telegramToken);

using CancellationTokenSource cts = new();

// StartReceiving does not block the caller thread. Receiving is done on the ThreadPool.
ReceiverOptions receiverOptions = new()
{
    AllowedUpdates = Array.Empty<UpdateType>() // receive all update types
};

botClient.StartReceiving(
    updateHandler: HandleUpdateAsync,
    pollingErrorHandler: HandlePollingErrorAsync,
    receiverOptions: receiverOptions,
    cancellationToken: cts.Token
);

var me = await botClient.GetMeAsync();
Console.WriteLine($"Start listening for @{me.Username}");

Console.ReadLine();

// Send cancellation request to stop bot
cts.Cancel();

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
    var completionResult = await openAiService.ChatCompletion.CreateCompletion(new ChatCompletionCreateRequest
    {
        Messages = new List<ChatMessage>
        {
            ChatMessage.FromUser(messageText),
        },
        Model = Models.ChatGpt3_5Turbo,
        MaxTokens = 50//optional
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