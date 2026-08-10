namespace GPTipsBot.Dtos
{
    public record UserChatKey(long Id, long ChatId, long? TelegramUserId = null)
    {
        public static implicit operator UserChatKey(long id)
            => new(id, id, id);
    }
}
