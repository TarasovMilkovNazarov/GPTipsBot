using GPTipsBot.Dtos;
using GPTipsBot.Enums;
using GPTipsBot.Localization;
using Tmessage = Telegram.Bot.Types.Message;

namespace GPTipsBot.Mapper
{
    public static class MessageMapper
    {
        public static MessageDto Map(Tmessage tMessage, long chatId, MessageOwner role)
        {
            MessageDto message = new()
            {
                TelegramMessageId = tMessage.MessageId,
                ChatId = chatId,
                Text = tMessage.Text,
                UserId = tMessage.From.Id,
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
