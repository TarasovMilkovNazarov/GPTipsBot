using GPTipsBot.Models;
using GPTipsBot.UpdateHandlers;
using Ardalis.GuardClauses;
using GPTipsBot.Dtos;
using Telegram.Bot;

namespace GPTipsBot.Extensions;

public static class UpdateDecoratorExtensions
{
    public static bool IsAdminCommand(this UpdateDecorator update)
    {
        return update.IsCommand && update.UserChatKey.IsAdmin() &&
               update.Command?.Type is CommandType.Admin or CommandType.Broadcast;
    }

    public static bool IsExpired(this UpdateDecorator update)
    {
        return update.Message.CreatedAt <= MainHandler.Start - TimeSpan.FromMinutes(2);
    }

    public static async Task<string> GetPhotoAsync(this UpdateDecorator update, ITelegramBotClient botClient)
    {
        Guard.Against.Null(update.FileId);

        return await update.FileId.GetPhotoAsync(botClient);
    }

    public static async Task<string> GetPhotoAsync(this string? fileId, ITelegramBotClient botClient)
    {
        Guard.Against.Null(fileId);

        var file = await botClient.GetFile(fileId);

        Guard.Against.Null(file.FilePath);

        using var memoryStream = new MemoryStream();
        await botClient.DownloadFile(file.FilePath, memoryStream);
        memoryStream.Position = 0;

        var base64String = Convert.ToBase64String(memoryStream.ToArray());

        return base64String;
    }
}