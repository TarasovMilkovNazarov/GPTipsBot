using System.Reflection;
using System.Text.RegularExpressions;
using Ardalis.GuardClauses;
using GPTipsBot.Dtos;
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
            Language = update.GetLanguageOrDefault();

            _update = update;
            var chatId = _update.Message?.Chat?.Id ??
                     _update.CallbackQuery?.Message?.Chat?.Id ??
                     _update.MyChatMember?.Chat?.Id;

            Guard.Against.Null(chatId, nameof(chatId));

            ChatId = chatId.Value;

            var oldChatMemberStatus = update.MyChatMember?.OldChatMember.Status;
            var newChatMemberStatus = update.MyChatMember?.NewChatMember.Status;
            if (oldChatMemberStatus == ChatMemberStatus.Kicked &&
                newChatMemberStatus == ChatMemberStatus.Member)
            {
                var chat = update.MyChatMember.Chat;

                User = new UserDto
                {
                    Id = chat.Id,
                    FirstName = chat.FirstName,
                    LastName = chat.LastName,
                };
                IsRecovered = true;
            }

            if (update.Message != null)
            {
                User = UserMapper.Map(update.Message.From);
                User.Source = TelegramService.GetSource(update.Message.Text);
                Message = MessageMapper.Map(update.Message, ChatId, Enums.MessageOwner.User);

                UserChatKey = new(User.Id, ChatId);
            }
            // if user press button below message like "stop request"
            else if (_update.CallbackQuery?.Message != null)
            {
                Message = MessageMapper.Map(_update.CallbackQuery.Message, ChatId, Enums.MessageOwner.User);
                Message.UserId = _update.CallbackQuery.From.Id;
                User = UserMapper.Map(_update.CallbackQuery.From);
                Message.Text = _update.CallbackQuery.Data;
            }

            UserChatKey ??= ChatId;

            var groupChatTypes = new ChatType?[] { ChatType.Supergroup, ChatType.Group, ChatType.Channel };
            IsGroupOrChannel = groupChatTypes.Contains(Message?.ChatType);

            if (update.Message?.Type == MessageType.Photo)
            {
                // Get the file id of the photo (the largest size)
                FileId = update.Message!.Photo[^1].FileId;
            }

            IsCommand = TryGetCommand(Message?.Text, out var command);
            Command = command;
        }

        public CustomBotCommand? Command { get; }

        public string? FileId { get; }

        public long ChatId { get; }

        public UserChatKey UserChatKey { get; }
        public UserDto User { get; }

        public MessageDto Message { get; }
        public bool IsRecovered { get; }
        public bool IsCommand { get; }
        public bool IsGroupOrChannel { get; }

        public CallbackQuery? CallbackQuery => _update.CallbackQuery;

        public string Language { get; }

        public override string ToString()
        {
            string serialized = JsonConvert.SerializeObject(_update, Formatting.Indented);

            return serialized;
        }

        private static bool TryGetCommand(string message, out CustomBotCommand? command)
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
