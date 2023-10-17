using GPTipsBot.Dtos;
using GPTipsBot.Mapper;
using GPTipsBot.Services;
using Newtonsoft.Json;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace GPTipsBot.UpdateHandlers
{
    public class UpdateDecorator
    {
        private Update _update;

        public UpdateDecorator(Update update, CancellationToken cancellationToken)
        {
            _update = update;
            ChatId = _update.Message?.Chat.Id ??
                _update.CallbackQuery?.Message?.Chat.Id ??
                _update.MyChatMember?.Chat.Id ??
                throw new ArgumentNullException(nameof(update), "Can't get ChatId");

            CancellationToken = cancellationToken;

            if (update.MyChatMember?.OldChatMember.Status == ChatMemberStatus.Kicked && update.MyChatMember?.NewChatMember.Status == ChatMemberStatus.Member)
            {
                var chat = update.MyChatMember.Chat;

                User = new UserDto()
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

            if (UserChatKey == null)
            {
                UserChatKey = new(ChatId, ChatId);
            }

            ServiceMessage = new MessageDto()
            {
                UserId = UserChatKey.Id,
                ChatId = UserChatKey.ChatId,
            };

            Reply = new MessageDto()
            {
                UserId = UserChatKey.Id,
                ChatId = UserChatKey.ChatId,
            };

            var groupChatTypes = new ChatType?[] { ChatType.Supergroup, ChatType.Group, ChatType.Channel };
            IsGroupOrChannel = groupChatTypes.Contains(Message?.ChatType);
        }

        public CancellationToken StatusTimerCancellationToken { get; set; }
        public CancellationToken CancellationToken { get; set; }

        public long ChatId { get; }

        public UserChatKey UserChatKey { get; internal set; }
        public UserDto User { get; set; }

        public MessageDto Message { get; set; }
        public MessageDto Reply { get; set; }
        public MessageDto ServiceMessage { get; set; }

        public ChatMemberStatus? ChatMemeberStatus => _update.MyChatMember?.NewChatMember.Status;
        public bool IsRecovered { get; }
        public bool IsGroupOrChannel { get; }

        public CallbackQuery? CallbackQuery => _update.CallbackQuery;

        public string Language => GetUserLanguage();


        string GetUserLanguage()
        {
            if (MainHandler.userState.ContainsKey(UserChatKey) && MainHandler.userState[UserChatKey].LanguageCode != null)
            {
                return MainHandler.userState[UserChatKey].LanguageCode;
            }

            return Message?.LanguageCode ?? "ru";
        }

        public override string ToString()
        {
            string serialized = JsonConvert.SerializeObject(_update, Formatting.Indented);

            return serialized;
        }
    }
}
