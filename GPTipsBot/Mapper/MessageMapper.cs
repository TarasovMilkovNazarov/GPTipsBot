using GPTipsBot.Dtos;
using GPTipsBot.Enums;
using GPTipsBot.Models;

namespace GPTipsBot.Mapper
{
    public static class MessageMapper
    {
        public static Message MapToMessage(TelegramGptMessageUpdate messageDto, MessageOwner role)
        {
            var message = new Message()
            {
                ChatId = messageDto.UserKey.ChatId,
                ReplyToId = messageDto.ReplyToId,
                Text = messageDto.Text,
                UserId = messageDto.UserKey.Id,
                CreatedAt = DateTime.UtcNow,
                Role = role
            };

            if (messageDto.ContextId.HasValue)
            {
                message.ContextId = messageDto.ContextId;
            }

            switch (role)
            {
                case MessageOwner.Assistant:
                    message.Text = messageDto.Reply;
                    message.ReplyToId = messageDto.MessageId;
                    break;
            }

            return message;
        }
    }
}
