using System.Reflection;
using Ardalis.GuardClauses;
using GPTipsBot.Dtos;
using GPTipsBot.Exceptions;
using GPTipsBot.Extensions;
using GPTipsBot.Mapper;
using GPTipsBot.Mappers;
using GPTipsBot.Services;
using Newtonsoft.Json;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.Payments;

namespace GPTipsBot.UpdateHandlers
{
    public class UpdateDecorator
    {
        public Update TelegramUpdate { get; }

        public UpdateDecorator(Update telegramUpdate)
        {
            TelegramUpdate = telegramUpdate;
            long chatId;

            switch (telegramUpdate.Type)
            {
                case UpdateType.PreCheckoutQuery:
                    Guard.Against.Null(telegramUpdate.PreCheckoutQuery);
                    UserChatKey = telegramUpdate.PreCheckoutQuery.From.Id;
                    User = UserMapper.Map(telegramUpdate.PreCheckoutQuery.From);
                    break;
                case UpdateType.Message when telegramUpdate.Message?.Chat.Type == ChatType.Private:
                    Guard.Against.Null(telegramUpdate.Message);
                    Guard.Against.Null(telegramUpdate.Message.From);

                    chatId = telegramUpdate.Message.Chat.Id;

                    User = UserMapper.Map(telegramUpdate.Message.From);
                    User.Source = TelegramService.GetSource(telegramUpdate.Message.Text);
                    Message = MessageMapper.Map(telegramUpdate.Message, chatId, Enums.MessageOwner.User);
                    UserChatKey = new UserChatKey(telegramUpdate.Message.From.Id, chatId);

                    if (telegramUpdate.Message.Type == MessageType.Photo)
                    {
                        Guard.Against.Null(telegramUpdate.Message.Photo);
                        FileId = telegramUpdate.Message.Photo[^1].FileId;
                    }

                    Message.SuccessfulPayment = telegramUpdate.Message.SuccessfulPayment;
                    break;
                case UpdateType.CallbackQuery:
                    Guard.Against.Null(telegramUpdate.CallbackQuery);
                    Guard.Against.Null(telegramUpdate.CallbackQuery.Message);
                    Guard.Against.Null(telegramUpdate.CallbackQuery.Data);
                    chatId = telegramUpdate.CallbackQuery.Message.Chat.Id;
                    Message = MessageMapper.Map(telegramUpdate.CallbackQuery.Message, chatId, Enums.MessageOwner.User);
                    Message.UserId = telegramUpdate.CallbackQuery.From.Id;
                    User = UserMapper.Map(telegramUpdate.CallbackQuery.From);
                    UserChatKey = new UserChatKey(telegramUpdate.CallbackQuery.From.Id, chatId);
                    Message.Text = telegramUpdate.CallbackQuery.Data;
                    break;
                case UpdateType.MyChatMember:
                    Guard.Against.Null(telegramUpdate.MyChatMember);
                    var oldChatMemberStatus = telegramUpdate.MyChatMember.OldChatMember.Status;
                    var newChatMemberStatus = telegramUpdate.MyChatMember.NewChatMember.Status;
                    if (oldChatMemberStatus == ChatMemberStatus.Kicked &&
                        newChatMemberStatus == ChatMemberStatus.Member)
                    {
                        User = UserMapper.Map(telegramUpdate.MyChatMember.From);
                        IsRecovered = true;
                        UserChatKey = new UserChatKey(telegramUpdate.MyChatMember.From.Id, telegramUpdate.MyChatMember.Chat.Id);
                    }
                    break;
                default:
                    throw new IgnoreMessageTypeException(telegramUpdate.Type);
            }

            Language = telegramUpdate.GetLanguageOrDefault();

            var groupChatTypes = new ChatType?[] { ChatType.Supergroup, ChatType.Group, ChatType.Channel };
            IsGroupOrChannel = groupChatTypes.Contains(Message?.ChatType);

            TryGetCommand(Message?.Text, out var command);
            Command = command;
        }

        public CustomBotCommand? Command { get; }

        public string? FileId { get; }

        public UserChatKey UserChatKey { get; }
        public UserDto User { get; }

        public MessageDto Message { get; }
        public bool IsRecovered { get; }
        public bool IsCommand => Command != null;
        public bool IsGroupOrChannel { get; }

        public CallbackQuery? CallbackQuery => TelegramUpdate.CallbackQuery;
        public PreCheckoutQuery? PreCheckoutQuery => TelegramUpdate.PreCheckoutQuery;

        public string Language { get; }

        public override string ToString()
        {
            var serialized = JsonConvert.SerializeObject(TelegramUpdate, Formatting.Indented);

            return serialized;
        }

        private static bool TryGetCommand(string? message, out CustomBotCommand? command)
        {
            command = null;
            if (string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            message = message.Trim().ToLower();

            var classType = typeof(BotMenu);
            var properties = classType.GetProperties(BindingFlags.Static | BindingFlags.Public)
                .Where(p => p.PropertyType == typeof(CustomBotCommand));

            foreach (var property in properties)
            {
                if (property.GetValue(null) is not CustomBotCommand botCommand) continue;

                var slashCommandValue = botCommand.Command;

                if (!message.StartsWith(slashCommandValue.ToLower()) &&
                    (!TelegramBotUiService.ButtonToLocalizations.ContainsKey(slashCommandValue) ||
                     !TelegramBotUiService.ButtonToLocalizations[slashCommandValue].Exists(b => b.ToLower() == message))) continue;
                command = botCommand;
                return true;
            }

            return false;
        }
    }
}
