namespace GPTipsBot.Dtos
{
    public class UserDto
    {
        public long Id { get; set; }
        public string FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Source { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public bool? IsActive { get; set; }
    }
}
