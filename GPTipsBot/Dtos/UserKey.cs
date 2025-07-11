namespace GPTipsBot.Dtos
{
    public record UserChatKey(long Id, long ChatId)
    {
        public static implicit operator UserChatKey(long id)
            => new(id, id);
    }
}
