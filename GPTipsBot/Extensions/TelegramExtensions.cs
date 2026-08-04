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


        public static User? GetUser(this Update? update)
        {
            if (update == null)
            {
                return null;
            }

            var user = update.Type switch
            {
                UpdateType.Message => update.Message?.From,
                UpdateType.ChatMember => update.ChatMember?.From,
                UpdateType.InlineQuery => update.InlineQuery?.From,
                UpdateType.ChosenInlineResult => update.ChosenInlineResult?.From,
                UpdateType.CallbackQuery => update.CallbackQuery?.From,
                UpdateType.EditedMessage => update.EditedMessage?.From,
                UpdateType.ChannelPost => update.ChannelPost?.From,
                UpdateType.EditedChannelPost => update.EditedChannelPost?.From,
                UpdateType.ShippingQuery => update.ShippingQuery?.From,
                UpdateType.PreCheckoutQuery => update.PreCheckoutQuery?.From,
                UpdateType.PollAnswer => update.PollAnswer?.User,
                UpdateType.MyChatMember => update.MyChatMember?.From,
                UpdateType.ChatJoinRequest => update.ChatJoinRequest?.From,
                _ => null
            };

            return user;
        }

        public static long? GetChatId(this Update? update)
        {
            if (update == null)
            {
                return null;
            }

            var userId = update.Type switch
            {
                UpdateType.Message => update.Message?.Chat.Id,
                UpdateType.ChatMember => update.ChatMember?.Chat.Id,
                UpdateType.InlineQuery => update.InlineQuery?.From.Id,
                UpdateType.ChosenInlineResult => update.ChosenInlineResult?.From.Id,
                UpdateType.CallbackQuery => update.CallbackQuery?.Message?.Chat.Id,
                UpdateType.EditedMessage => update.EditedMessage?.Chat?.Id,
                UpdateType.ChannelPost => update.ChannelPost?.Chat?.Id,
                UpdateType.EditedChannelPost => update.EditedChannelPost?.Chat?.Id,
                UpdateType.ShippingQuery => update.ShippingQuery?.From.Id,
                UpdateType.PreCheckoutQuery => update.PreCheckoutQuery?.From.Id,
                UpdateType.PollAnswer => update.PollAnswer?.User.Id,
                UpdateType.MyChatMember => update.MyChatMember?.Chat.Id,
                UpdateType.ChatJoinRequest => update.ChatJoinRequest?.Chat.Id,
                _ => null
            };

            return userId;
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
                case MessageType.Voice:
                case MessageType.Photo:
                case MessageType.SuccessfulPayment:
                    return false;
                case MessageType.Video:
                case MessageType.Audio:
                    throw new NotSupportedMessageException(update.Message.Chat.Id, update.Message.Type.ToString());
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
