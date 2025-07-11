using GPTipsBot.Enums;

namespace GPTipsBot.Dtos
{
    public class UserStateDto
    {
        public Dictionary<long, CancellationTokenSource> MessageIdToCancellation = new();
    }
}
