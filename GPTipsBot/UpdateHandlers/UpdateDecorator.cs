using GPTipsBot.Dtos;
using GPTipsBot.Mapper;
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
                throw new ArgumentNullException();

            CancellationToken = cancellationToken;

            if (update.Message != null)
            {
                User = UserMapper.Map(update.Message.From);
                Message = MessageMapper.Map(update.Message, ChatId, Enums.MessageOwner.User);

                ServiceMessage = new MessageDto()
                {
                    UserId = User.Id,
                    ChatId = ChatId,
                };

                UserChatKey = new(User.Id, ChatId);
            }
            else if (_update.CallbackQuery?.Message != null)
            {
                //User = UserMapper.Map(_update.CallbackQuery?.Message.From);
                Message = MessageMapper.Map(_update.CallbackQuery?.Message, ChatId, Enums.MessageOwner.User);
                Message.UserId = _update.CallbackQuery.Message.Chat.Id;
                Message.Text = _update.CallbackQuery.Data;
            }

            if (UserChatKey == null)
            {
                UserChatKey = new(ChatId, ChatId);
            }

            Reply = new MessageDto()
            {
                UserId = User.Id,
                ChatId = ChatId,
            };
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

        public CallbackQuery? CallbackQuery => _update.CallbackQuery;


        public string GetUserLanguage()
        {
            if (MainHandler.userState.ContainsKey(UserChatKey))
            {
                return MainHandler.userState[UserChatKey].LanguageCode;
            }

            return Message?.LanguageCode ?? "ru";
        }
    }
}
