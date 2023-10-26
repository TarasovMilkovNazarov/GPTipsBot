using GPTipsBot.Models;
using Microsoft.EntityFrameworkCore;

namespace GPTipsBot.Db
{
    public sealed class ApplicationContext : DbContext
    {
        public Guid Guid { get; } = Guid.NewGuid();
        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Message> Messages { get; set; } = null!;
        public DbSet<BotSettings> BotSettings { get; set; } = null!;
        public DbSet<OpenaiAccount> OpenaiAccounts { get; set; } = null!;

        public ApplicationContext()
        {
            Database.EnsureCreated();
        }
        
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var connectionString = Environment.GetEnvironmentVariable("PG_CONNECTION_STRING");

            optionsBuilder.UseNpgsql(connectionString);
        }

        //protected override void OnModelCreating(ModelBuilder modelBuilder)
        //{
        //    modelBuilder.Entity<User>()
        //        .HasOne(m => m.BotSettings)
        //        .WithOne("BotSettingsId")
        //        .OnDelete(DeleteBehavior.Cascade);
        //}
    }
}
