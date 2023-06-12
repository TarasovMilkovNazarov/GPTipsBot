using GPTipsBot.Enums;

namespace GPTipsBot.Dtos
{
    public class UserStateDto
    {
        public UserStateDto(UserChatKey userKey)
        {
            CurrentState = UserStateEnum.None;
            UserKey = userKey;
        }

        public UserChatKey UserKey { get; }
        public string LanguageCode { get; set; }
        public UserStateEnum CurrentState { get; set; }
        public Dictionary<long, CancellationTokenSource> messageIdToCancellation = new ();
    }
}
