using GPTipsBot.Extensions;
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
            var botClient = new TelegramBotClient(new TelegramBotClientOptions(AppConfig.TelegramToken));
            foreach (var adminId in AppConfig.AdminIds)
            {
                var updateIdQuery = logEvent.Properties.TryGetValue("updateId", out var updateId)
                    ? "?query=json_payload.updateId+%3D+" + updateId
                    : "";
                
                var text = $"""
🚧🚧🚧 ПАРДОН МЕСЬЕ Я ПРИУНЫЛ:
{logEvent.RenderMessage().Truncate(1000)}
Подробнее: https://console.cloud.yandex.ru/folders/b1ghg7fp1esojrsq87tq/logging/group/e23pildlggn1clcjtr5u/logs{updateIdQuery}
""";
                botClient.SendTextMessageAsync(adminId, text).GetAwaiter().GetResult();
            }
        }
        catch (Exception e)
        {
            Log.Logger.Fatal(new TelegramErrorReportException(e), "Не смогли отправить сообщение об ошибке в телеграмм");
        }
    }
}