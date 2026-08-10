using GPTipsBot.Config;
using GPTipsBot.Dtos;

namespace GPTipsBot.Extensions;

public static class UserChatKeyExtensions
{
    public static bool IsAdmin(this UserChatKey userChatKey) =>
        AppConfig.AdminIds.Contains(userChatKey.TelegramUserId ?? userChatKey.Id);
}