using System.ComponentModel.DataAnnotations.Schema;

namespace GPTipsBot.Models
{
    public class User
    {
        public long Id { get; set; }
        public string FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Source { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public bool IsActive { get; set; } = true;
        public BotSettings? BotSettings { get; set; }

        [ForeignKey("BotSettings")]
        public long? BotSettingsId { get; set; }

        public List<UserCommand> Commands { get; set; }
        public int FreeImageGenerations { get; set; } = 5;
        public int FreeImageTextRecognitions { get; set; } = 5;
        public int FreeGptRequests { get; set; } = 10;
        public Wallet? Wallet { get; set; }
        [ForeignKey("Wallet")]
        public long? WalletId { get; set; }
    }
}
