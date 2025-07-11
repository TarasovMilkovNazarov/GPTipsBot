using GPTipsBot.Dtos;
using GPTipsBot.Enums;
using GPTipsBot.Localization;
using Tmessage = Telegram.Bot.Types.Message;

namespace GPTipsBot.Mappers
{
    public static class MessageMapper
    {
        public static MessageDto Map(Tmessage tMessage, long chatId, MessageOwner role)
        {
            UserChatKey chatKey = new UserChatKey(tMessage.From.Id, chatId);

            MessageDto message = new(chatKey)
            {
                TelegramMessageId = tMessage.MessageId,
                Text = tMessage.Text,
                Type = tMessage.Type,
                CreatedAt = tMessage.Date,
                Role = role,
                EntityValues = tMessage.EntityValues,
                Entities = tMessage.Entities,
                ReplyToMessage = tMessage.ReplyToMessage,
                ChatType = tMessage.Chat.Type,
                ContextBound = true,
                LanguageCode = LocalizationManager.GetCulture(tMessage.From?.LanguageCode).TwoLetterISOLanguageName
            };

            return message;
        }
    }
}
