using GPTipsBot.Db;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using User = GPTipsBot.Models.User;

namespace GPTipsBot.Repositories
{
    public class UserRepository(ILogger<UserRepository> logger, ApplicationContext context, IMemoryCache memoryCache)
    {
        public const string CacheKeyPrefix = "user_";

        public bool Any(long id)
        {
            return context.Users.Any(x => x.Id == id);
        }

        public User? Get(long id)
        {
            return context.Users
                .Include(u => u.Wallet)
                .SingleOrDefault(x => x.Id == id);
        }

        public void Delete(long id)
        {
            var user = context.Users.FirstOrDefault(x => x.Id == id);

            if (user == null)
            {
                throw new Exception($"User id={id} not found");
            }

            context.Users.Remove(user);
        }

        public async Task<long> Create(User user)
        {
            logger.LogInformation("CreateUser");
            var entity = context.Users.Add(user).Entity;
            await context.SaveChangesAsync();

            return user.Id;
        }

        public async Task Update(User newUser)
        {
            var dbUser = Get(newUser.Id);

            if (dbUser == null) { throw new ArgumentNullException($"Can't find user with Id {newUser.Id}"); }

            dbUser.FirstName = newUser.FirstName;
            dbUser.LastName = newUser.LastName;
            dbUser.IsActive = newUser.IsActive;
            dbUser.Source = newUser.Source ?? dbUser.Source;

            await context.SaveChangesAsync();
        }

        public long GetActiveUsersCount()
        {
            return context.Users.AsNoTracking().Count(x => x.IsActive);
        }

        public async Task<bool> SoftlyRemoveUser(long telegramId)
        {
            var user = Get(telegramId);
            if (user == null) return false;

            user.IsActive = false;
            // todo добавить UpdateAt поле для подсчета удаливших бота юзеров
            await context.SaveChangesAsync();

            memoryCache.Remove(CacheKeyPrefix+telegramId);
            return true;
        }
    }
}
