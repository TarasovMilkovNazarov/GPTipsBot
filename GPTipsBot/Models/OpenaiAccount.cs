﻿namespace GPTipsBot.Models
{
    public class OpenaiAccount:Entity
    {
        public string Token { get; set; }
        public string? OrganizationId { get; set; }
        public string? Email { get; set; }
        public string? Password { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? FreezedAt { get; set; }
        public double? Balance { get; set; }
        public bool IsDeleted { get; set; }
        public DeletionReason DeletionReason { get; set; }
    }

    public enum DeletionReason
    {
        None,
        Deactivated,
        MonthLimit,
        InsufficientQuota
    }
}
