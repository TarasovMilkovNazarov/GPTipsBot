using System.Reflection;
using GPTipsBot.Dtos;
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
            ChatId = _update.Message?.Chat?.Id ??
                _update.CallbackQuery?.Message?.Chat?.Id ??
                _update.MyChatMember?.Chat?.Id ??
                throw new ArgumentNullException(nameof(update), "Can't get ChatId");

            var oldChatMemberStatus = update.MyChatMember?.OldChatMember.Status;
            var newChatMemberStatus = update.MyChatMember?.NewChatMember.Status;
            if (oldChatMemberStatus == Telegram.Bot.Types.Enums.ChatMemberStatus.Kicked && 
                newChatMemberStatus == Telegram.Bot.Types.Enums.ChatMemberStatus.Member)
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
            else if (_update.CallbackQuery?.Message != null)
            {
                //User = UserMapper.Map(_update.CallbackQuery?.Message.From);
                Message = MessageMapper.Map(_update.CallbackQuery.Message, ChatId, Enums.MessageOwner.User);
                Message.UserId = _update.CallbackQuery.Message.Chat.Id;
                Message.Text = _update.CallbackQuery.Data;
            }

            UserChatKey ??= new(ChatId, ChatId);

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

        public CustomBotCommand? Command { get; set; }

        public string FileId { get; set; }

        public CancellationToken StatusTimerCancellationToken { get; set; }

        public long ChatId { get; }

        public UserChatKey UserChatKey { get; }
        public UserDto User { get; set; }

        public MessageDto Message { get; set; }

        public ChatMemberStatus? ChatMemberStatus => _update.MyChatMember?.NewChatMember.Status;
        public bool IsRecovered { get; }
        public bool IsCommand { get; set; }
        public bool IsGroupOrChannel { get; }

        public CallbackQuery? CallbackQuery => _update.CallbackQuery;

        public string Language => GetUserLanguage();


        string GetUserLanguage()
        {
            if (MainHandler.UserState.ContainsKey(UserChatKey) && MainHandler.UserState[UserChatKey].LanguageCode != null)
            {
                return MainHandler.UserState[UserChatKey].LanguageCode;
            }

            return Message?.LanguageCode ?? "ru";
        }

        public override string ToString()
        {
            string serialized = JsonConvert.SerializeObject(_update, Formatting.Indented);

            return serialized;
        }

        private bool TryGetCommand(string message, out CustomBotCommand? command)
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
