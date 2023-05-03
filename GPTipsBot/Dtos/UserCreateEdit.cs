using GPTipsBot.Services;

namespace GPTipsBot.Dtos
{
    public class CreateEditUser
    {
        public CreateEditUser(Telegram.Bot.Types.Message message)
        {
            Id = message.From.Id;
            TelegramId = message.From.Id;
            FirstName = message.From.FirstName;
            LastName = message.From.LastName;
            Message = message.Text;
            TimeStamp = DateTime.UtcNow;
        }

        public long Id { get; set; }
        public string FirstName { get; set; }
        public string? LastName { get; set; }
        public long TelegramId { get; set; }
        public string Message { get; set; }
        public DateTimeOffset TimeStamp { get; set; }
        public bool IsActive { get; set; } = true;
        public string? Source { get; set; }
    }
}
