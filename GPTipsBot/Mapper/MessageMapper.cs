using GPTipsBot.Dtos;
using GPTipsBot.Models;
using Polly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace GPTipsBot.Mapper
{
    public static class MessageMapper
    {
        public static Message MapToMessage(TelegramGptMessageUpdate messageDto, MessageOwner role)
        {
            Message message = new Message()
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
