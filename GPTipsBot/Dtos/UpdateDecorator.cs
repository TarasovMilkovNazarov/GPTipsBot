using System.Reflection;
using System.Text.RegularExpressions;
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

namespace GPTipsBot.UpdateHandlers
{
    public class UpdateDecorator
    {
        private Update _update;

        public UpdateDecorator(Update update)
        {
            _update = update;
            long chatId;

            switch (update.Type)
            {
                case UpdateType.Message when update.Message?.Chat.Type == ChatType.Private:
                    Guard.Against.Null(update.Message);
                    Guard.Against.Null(update.Message.From);

                    chatId = update.Message.Chat.Id;

                    User = UserMapper.Map(update.Message.From);
                    User.Source = TelegramService.GetSource(update.Message.Text);
                    Message = MessageMapper.Map(update.Message, chatId, Enums.MessageOwner.User);
                    UserChatKey = new UserChatKey(update.Message.From.Id, chatId);

                    if (update.Message.Type == MessageType.Photo)
                    {
                        Guard.Against.Null(update.Message.Photo);
                        FileId = update.Message.Photo[^1].FileId;
                    }
                    break;
                case UpdateType.CallbackQuery:
                    Guard.Against.Null(update.CallbackQuery);
                    Guard.Against.Null(update.CallbackQuery.Message);
                    Guard.Against.Null(update.CallbackQuery.Data);
                    chatId = update.CallbackQuery.Message.Chat.Id;
                    Message = MessageMapper.Map(update.CallbackQuery.Message, chatId, Enums.MessageOwner.User);
                    Message.UserId = update.CallbackQuery.From.Id;
                    User = UserMapper.Map(update.CallbackQuery.From);
                    UserChatKey = new UserChatKey(update.CallbackQuery.From.Id, chatId);
                    Message.Text = update.CallbackQuery.Data;
                    break;
                case UpdateType.MyChatMember:
                    Guard.Against.Null(update.MyChatMember);
                    var oldChatMemberStatus = update.MyChatMember.OldChatMember.Status;
                    var newChatMemberStatus = update.MyChatMember.NewChatMember.Status;
                    if (oldChatMemberStatus == ChatMemberStatus.Kicked &&
                        newChatMemberStatus == ChatMemberStatus.Member)
                    {
                        User = UserMapper.Map(update.MyChatMember.From);
                        IsRecovered = true;
                    }
                    break;
                default:
                    throw new IgnoreMessageTypeException(update.Type);
            }

            Language = update.GetLanguageOrDefault();

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

        public CallbackQuery? CallbackQuery => _update.CallbackQuery;

        public string Language { get; }

        public override string ToString()
        {
            var serialized = JsonConvert.SerializeObject(_update, Formatting.Indented);

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
