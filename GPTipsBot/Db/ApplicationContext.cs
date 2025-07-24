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
            Guid = Guid.NewGuid();
        }
        
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseNpgsql(AppConfig.ConnectionString);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // modelBuilder.Entity<User>()
            //     .HasOne(u => u.Wallet)
            //     .WithOne(w => w.User)
            //     .HasForeignKey<Wallet>(w => w.UserId)
            //     .OnDelete(DeleteBehavior.Cascade);
            //
            // modelBuilder.Entity<Wallet>()
            //     .HasOne(m => m.User)
            //     .WithOne(u => u.Wallet)
            //     .HasForeignKey<User>(m => m.WalletId);
        }
    }
}
