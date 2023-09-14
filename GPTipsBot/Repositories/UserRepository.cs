using GPTipsBot.Db;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Data;
using User = GPTipsBot.Models.User;

namespace GPTipsBot.Repositories
{
    public class UserRepository
    {
        private readonly ILogger<TelegramBotWorker> logger;
        private readonly ApplicationContext context;

        public UserRepository(ILogger<TelegramBotWorker> logger, ApplicationContext context)
        {
            this.logger = logger;
            this.context = context;
        }

        public User? Get(long id)
        {
            return context.Users.FirstOrDefault(x => x.Id == id);
        }

        public long Create(User user)
        {
            logger.LogInformation("CreateUser");

            return context.Users.Add(user).Entity.Id;
        }

        public void Update(User user)
        {
            var dbUser = Get(user.Id);
            if (dbUser == null)
            {
                throw new InvalidOperationException($"Can't find user with id {user.Id}");
            }

            context.Users.Update(user);
        }
        
        public IEnumerable<User> GetAll()
        {
            return context.Users.AsNoTracking().ToList();
        }

        public long GetActiveUsersCount()
        {
            return context.Users.AsNoTracking().Where(x => x.IsActive).Count();
        }

        public long SoftlyRemoveUser(long telegramId)
        {
            return context.Users.Where(x => x.Id == telegramId).ExecuteUpdate(x => x.SetProperty(y => y.IsActive, false));
        }
    }
}
