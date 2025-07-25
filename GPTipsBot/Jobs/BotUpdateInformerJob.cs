using GPTipsBot.Db;
using Quartz;
using Telegram.Bot;
using Telegram.Bot.Exceptions;

namespace GPTipsBot.Jobs;

public class BotUpdateInformerJob(ApplicationContext context, ITelegramBotClient botClient) : IJob
{
    private readonly ITelegramBotClient _botClient = botClient;

    public async Task Execute(IJobExecutionContext context1)
    {
        var botClient = new TelegramBotClient("");

        var message =
            "🎵 Новый крутой функционал в боте! 🎧\n\nТеперь вы можете создавать уникальную музыку просто из текста! 🎶✨\n\n" +
            "🔹 Как это работает?\n\n    ✨ Шаг 1. Введите команду /music и придумайте описание мелодии (например, \"космический синтвейв, 120 bpm\")\n\n" +
            "    🎹 Шаг 2. Бот сгенерирует трек по вашему запросу\n\n    📥 Шаг 3. Скачивайте и делитесь крутыми битами!\n\n" +
            "🚀 Попробуйте прямо сейчас! Просто отправьте боту команду /music или нажмите кнопку в меню.\n\n Стоимость генерации 10⭐️";

        var users = context.Users.Select(u => u.Id).ToList();

        const int messagesPerSecond = 25;
        var delayPerMessage = TimeSpan.FromMilliseconds(1000 / messagesPerSecond);

        foreach (var user in users)
        {
            try
            {
                await botClient.SendMessage(user, message);
                await Task.Delay(delayPerMessage);
            }
            catch (ApiRequestException ex) when (ex.ErrorCode == 403)
            {
                var userToDelete = await context.Users.FindAsync(user);
                if (userToDelete != null)
                    userToDelete.IsActive = false;
            }
            catch (Exception e)
            {
                // ignore
            }
        }
        await context.SaveChangesAsync();
    }
}