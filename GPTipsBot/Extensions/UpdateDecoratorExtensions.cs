using GPTipsBot.Models;
using GPTipsBot.UpdateHandlers;

namespace GPTipsBot.Extensions;

public static class UpdateDecoratorExtensions
{
    public static bool IsAdminCommand(this UpdateDecorator update)
    {
        return update.IsCommand && update.UserChatKey.IsAdmin() && update.Command?.Type == CommandType.Admin;
    }

    public static bool IsExpired(this UpdateDecorator update)
    {
        return update.Message.CreatedAt <= FirstUpdateHandler.Start - TimeSpan.FromMinutes(2);
    }
}