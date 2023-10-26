using Serilog;
using Serilog.Core;
using Serilog.Events;
using Telegram.Bot;

namespace GPTipsBot.Logging;

public class TelegramSink : ILogEventSink
{
    public void Emit(LogEvent logEvent)
    {
        try
        {
            var botClient = new TelegramBotClient(new TelegramBotClientOptions(AppConfig.TelegramToken));
            foreach (var adminId in AppConfig.AdminIds)
            {
                var text = $"""
🚧🚧🚧 ПАРДОН МЕСЬЕ Я ПРИУНЫЛ:
{logEvent.RenderMessage()}
Подробнее: https://console.cloud.yandex.ru/folders/b1ghg7fp1esojrsq87tq/logging/group/e23pildlggn1clcjtr5u/logs
""";
                botClient.SendTextMessageAsync(adminId, text).GetAwaiter().GetResult();
            }
        }
        catch (Exception e)
        {
            Log.Logger.Fatal(e, "Не смогли отправить сообщение об ошибке в телеграмм");
        }
    }
}