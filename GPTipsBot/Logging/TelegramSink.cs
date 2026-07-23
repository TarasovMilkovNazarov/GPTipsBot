using GPTipsBot.Config;
using GPTipsBot.Extensions;
using GPTipsBot.Services;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Telegram.Bot;

namespace GPTipsBot.Logging;

public class TelegramSink : ILogEventSink
{
    public void Emit(LogEvent logEvent)
    {
        if (logEvent.Exception is TelegramErrorReportException)
            return;

        try
        {
            using var httpClient = new HttpClient(new HappProxyClientHandler());
            var botClient = new TelegramBotClient(AppConfig.TelegramToken, httpClient);
            foreach (var adminId in AppConfig.AdminIds)
            {
                var text = $"""
🚧🚧🚧 Я УПАЛ, иди чини:
Подробнее: https://momskibana.milkov.uk/app/r/s/80eMV
{logEvent.RenderMessage().Truncate(1000)}
""";
                botClient.SendMessage(adminId, text).GetAwaiter().GetResult();
            }
        }
        catch (Exception e)
        {
            Log.Logger.Fatal(new TelegramErrorReportException(e), "Не смогли отправить сообщение об ошибке в телеграмм");
        }
    }
}