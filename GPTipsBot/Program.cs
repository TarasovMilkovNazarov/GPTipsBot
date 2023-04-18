using OpenAI.GPT3.Managers;
using OpenAI.GPT3;
using RestSharp;
using OpenAI.GPT3.ObjectModels;
using GPTipsBot;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var hostBuilder = new HostBuilder()
        // Add configuration, logging, ...
    .ConfigureServices((hostContext, services) =>
    {
        // Add your services with depedency injection.
        services
        .AddLogging(configure => configure.AddConsole())
        .AddHostedService<TelegramBotWorker>();
    });

await hostBuilder.RunConsoleAsync();

const string telegramToken = "6272630353:AAG6zDC3BTBQ0dt09nHE6_mN4RpDRUEjPDM";
const long chatId = 486363646;
const string openaiBaseUrl = "https://api.openai.com/v1/engines/davinci-codex";

var openAiHttpClient = new RestClient(openaiBaseUrl);

// Define the message to send to the GPT chat
var message = "send only letter which comes after \"d\" in the alphabet";

var openAiService = new OpenAIService(new OpenAiOptions()
{
    //ApiKey =  Environment.GetEnvironmentVariable("MY_OPEN_AI_API_KEY"),
    ApiKey = "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"
});
openAiService.SetDefaultModelId(Models.Davinci);

var tgBotApi = new TelegramBotAPI(telegramToken, chatId);
tgBotApi.SetMyDescription("DescriptionExample");