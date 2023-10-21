using GPTipsBot.Dtos;

namespace GPTipsBot.Extensions;

public static class UserChatKeyExtensions
{
    public static bool IsAdmin(this UserChatKey userChatKey) => AppConfig.AdminIds.Contains(userChatKey.Id);
}