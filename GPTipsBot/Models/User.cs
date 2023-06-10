namespace GPTipsBot.Models
{
    public class User
    {
        public long Id { get; set; }
        public string FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Source { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public bool? IsActive { get; set; }
        public long MessagesCount { get; set; }
        public List<Message> Messages { get; set; }
    }
}
