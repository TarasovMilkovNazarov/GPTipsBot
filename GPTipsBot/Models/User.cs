using System.ComponentModel.DataAnnotations.Schema;
using GPTipsBot.Config;

namespace GPTipsBot.Models
{
    public class User
    {
        public long Id { get; set; }
        /// <summary>Telegram user id when linked; null for email/guest-only accounts.</summary>
        public long? TelegramId { get; set; }
        /// <summary>Yandex ID (login.yandex.ru/info id) when linked; null otherwise.</summary>
        public string? YandexId { get; set; }
        public string FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Source { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public bool IsActive { get; set; } = true;
        public BotSettings? BotSettings { get; set; }

        [ForeignKey("BotSettings")]
        public long? BotSettingsId { get; set; }

        public List<UserCommand> Commands { get; set; }
        public int FreeImageGenerations { get; set; } = PaymentConfig.NewbieFreeImageGenerations;
        public int FreeCombinePhotos { get; set; } = PaymentConfig.NewbieFreeCombinePhotos;
        public int FreeChangePhotos { get; set; } = PaymentConfig.NewbieFreeChangePhotos;
        public int FreeImageTextRecognitions { get; set; } = PaymentConfig.NewbieFreeTextRecognitions;
        public int FreeGptRequests { get; set; } = PaymentConfig.NewbieFreeChatGptRequests;
        public int FreePhotoAnimations { get; set; } = PaymentConfig.NewbieFreePhotoAnimations;
        public int FreeSummaryRequests { get; set; } = PaymentConfig.NewbieFreeSummaries;
        public Wallet? Wallet { get; set; }
        [ForeignKey("Wallet")]
        public long? WalletId { get; set; }

        /// <summary>Normalized email for web email auth (unique when set).</summary>
        public string? Email { get; set; }
        public string? PasswordHash { get; set; }
        public bool EmailConfirmed { get; set; }
        public string? EmailConfirmCode { get; set; }
        public DateTimeOffset? EmailConfirmExpiresAt { get; set; }
    }
}
