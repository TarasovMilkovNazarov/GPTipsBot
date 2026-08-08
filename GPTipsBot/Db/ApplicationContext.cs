using GPTipsBot.Config;
using GPTipsBot.Models;
using Microsoft.EntityFrameworkCore;

namespace GPTipsBot.Db
{
    public sealed class ApplicationContext : DbContext
    {
        public Guid Guid { get; }
        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Message> Messages { get; set; } = null!;
        public DbSet<BotSettings> BotSettings { get; set; } = null!;
        public DbSet<OpenaiAccount> OpenaiAccounts { get; set; } = null!;
        public DbSet<UserCommand> UserCommands { get; set; } = null!;
        public DbSet<Transaction> Transactions { get; set; } = null!;
        public DbSet<Invoice> Invoices { get; set; } = null!;
        public DbSet<Wallet> Wallets { get; set; } = null!;

        public ApplicationContext()
        {
            Database.EnsureCreated();
            EnsureYooKassaInvoiceColumns();
            EnsureFreePhotoAnimationsColumn();
            EnsureFreeSummaryRequestsColumn();
            Guid = Guid.NewGuid();
        }
        
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseNpgsql(AppConfig.ConnectionString);
        }

        /// <summary>
        /// EnsureCreated does not alter existing tables; add YooKassa columns idempotently.
        /// </summary>
        private void EnsureYooKassaInvoiceColumns()
        {
            Database.ExecuteSqlRaw("""
                ALTER TABLE "Invoices" ADD COLUMN IF NOT EXISTS "Provider" integer NOT NULL DEFAULT 0;
                ALTER TABLE "Invoices" ADD COLUMN IF NOT EXISTS "ExternalPaymentId" text NULL;
                ALTER TABLE "Invoices" ADD COLUMN IF NOT EXISTS "FiatAmountKopecks" bigint NULL;
                """);
        }

        /// <summary>
        /// EnsureCreated does not alter existing tables; add free animation counter idempotently.
        /// </summary>
        private void EnsureFreePhotoAnimationsColumn()
        {
            Database.ExecuteSqlRaw($"""
                ALTER TABLE "Users" ADD COLUMN IF NOT EXISTS "FreePhotoAnimations" integer NOT NULL DEFAULT {PaymentConfig.NewbieFreePhotoAnimations};
                """);
        }

        private void EnsureFreeSummaryRequestsColumn()
        {
            Database.ExecuteSqlRaw($"""
                ALTER TABLE "Users" ADD COLUMN IF NOT EXISTS "FreeSummaryRequests" integer NOT NULL DEFAULT {PaymentConfig.NewbieFreeSummaries};
                """);
        }
    }
}
