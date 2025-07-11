using GPTipsBot.Exceptions;
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

        public static string GetLanguageOrDefault(this Update update, string language = "ru")
        {
            return update.Message?.From?.LanguageCode ??
                   update.CallbackQuery?.Message?.From?.LanguageCode ??
                   update.ChatMember?.From?.LanguageCode ??
                   update.ChannelPost?.From?.LanguageCode ?? language;
        }

        public static bool Ignore(this Update update)
        {
            if (update.EditedMessage != null)
            {
                return true;
            }

            switch (update.Message?.Type)
            {
                case null:
                    break;
                case MessageType.Text:
                case MessageType.Audio:
                case MessageType.Voice:
                case MessageType.Photo:
                    return false;
                case MessageType.Video:
                    throw new NotSupportedMessageException(update.Message.Type.ToString());
                default:
                    return true;
            }

            var botsChatStatus = update.MyChatMember;

            if (botsChatStatus == null) { return false; }

            var oldStatus = botsChatStatus.OldChatMember.Status;
            var newStatus = botsChatStatus.NewChatMember.Status;

            switch (newStatus)
            {
                case ChatMemberStatus.Left:
                case ChatMemberStatus.Administrator:
                case ChatMemberStatus.Kicked:
                case ChatMemberStatus.Member when oldStatus == ChatMemberStatus.Left:
                    return true;
            }

            return oldStatus == ChatMemberStatus.Administrator;
        }
    }
}
