using GPTipsBot.Dtos;
using GPTipsBot.Models;
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
        public static Message MapToMessage(TelegramGptMessageUpdate messageDto, GptRolesEnum role)
        {
            Message message = new Message()
            {
                ChatId = messageDto.ChatId,
                ContextId = messageDto.ContextId ?? 0,
                ReplyToId = messageDto.ReplyToId,
                Text = messageDto.Text,
                UserId = messageDto.TelegramId,
                CreatedAt = DateTime.UtcNow,
                Role = role
            };

            switch (role)
            {
                case GptRolesEnum.Assistant:
                    message.Text = messageDto.Reply;
                    message.ReplyToId = messageDto.MessageId;
                    break;
            }

            return message;
        }
    }
}
