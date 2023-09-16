using GPTipsBot.Models;
using Microsoft.EntityFrameworkCore;

namespace GPTipsBot.Db
{
    public class ApplicationContext : DbContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<BotSettings> BotSettings { get; set; }
        public DbSet<OpenaiAccount> OpenaiAccounts { get; set; }
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
