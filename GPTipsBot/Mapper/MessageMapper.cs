using GPTipsBot.Dtos;
using GPTipsBot.Enums;
using GPTipsBot.Models;
using GPTipsBot.UpdateHandlers;
using Polly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;
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
                ContextBound = true
            };

            return message;
        }
    }
}
