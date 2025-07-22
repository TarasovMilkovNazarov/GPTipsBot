using GPTipsBot.Db;
using GPTipsBot.Enums;
using Microsoft.EntityFrameworkCore;
using Quartz;
using Telegram.Bot;
using Telegram.Bot.Exceptions;

namespace GPTipsBot.Dtos;

public class BotUpdateInformerJob : IJob
{
    private readonly ApplicationContext _context;
    private readonly ITelegramBotClient _botClient;

    public BotUpdateInformerJob(ApplicationContext context, ITelegramBotClient botClient)
    {
        _context = context;
        _botClient = botClient;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var botClient = new TelegramBotClient("");

        var message =
            "🎵 Новый крутой функционал в боте! 🎧\n\nТеперь вы можете создавать уникальную музыку просто из текста! 🎶✨\n\n" +
            "🔹 Как это работает?\n\n    ✨ Шаг 1. Введите команду /music и придумайте описание мелодии (например, \"космический синтвейв, 120 bpm\")\n\n" +
            "    🎹 Шаг 2. Бот сгенерирует трек по вашему запросу\n\n    📥 Шаг 3. Скачивайте и делитесь крутыми битами!\n\n" +
            "🚀 Попробуйте прямо сейчас! Просто отправьте боту команду /music или нажмите кнопку в меню.\n\n Стоимость генерации 10⭐️";

        var users = _context.Users.Select(u => u.Id).ToList();

        const int messagesPerSecond = 25;
        var delayPerMessage = TimeSpan.FromMilliseconds(1000 / messagesPerSecond);

        foreach (var user in users)
        {
            try
            {
                await botClient.SendTextMessageAsync(user, message);
                await Task.Delay(delayPerMessage);
            }
            catch (ApiRequestException ex) when (ex.ErrorCode == 403)
            {
                var userToDelete = await _context.Users.FindAsync(user);
                if (userToDelete != null)
                    userToDelete.IsActive = false;
            }
            catch (Exception e)
            {
                // ignore
            }
        }
        await _context.SaveChangesAsync();
    }
}