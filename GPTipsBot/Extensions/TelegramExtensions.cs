using Newtonsoft.Json;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace GPTipsBot.Extensions
{
    public static class TelegramExtensions
    {
        public static string Serialize(this Update update)
        {
            return JsonConvert.SerializeObject(update, Formatting.Indented);
        }

        public static bool Ignore(this Update update)
        {
            if (update.EditedMessage != null)
            {
                return true;
            }

            var botsChatStatus = update.MyChatMember;

            if (botsChatStatus == null) { return false; }

            var oldStatus = botsChatStatus.OldChatMember.Status;
            var newStatus = botsChatStatus.NewChatMember.Status;

            if (newStatus == ChatMemberStatus.Administrator)
            {
                return true;
            }
            if (oldStatus == ChatMemberStatus.Administrator)
            {
                return true;
            }
            if (newStatus == ChatMemberStatus.Left)
            {
                return true;
            }
            if (oldStatus == ChatMemberStatus.Left && 
                newStatus == ChatMemberStatus.Member)
            {
                return true;
            }

            return false;
        } 
    }
}
