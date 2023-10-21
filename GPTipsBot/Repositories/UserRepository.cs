using GPTipsBot.Db;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using User = GPTipsBot.Models.User;

namespace GPTipsBot.Repositories
{
    public class UserRepository
    {
        private readonly ILogger<UserRepository> logger;
        private readonly ApplicationContext context;
        public Guid Guid { get; } = Guid.NewGuid();

        public UserRepository(ILogger<UserRepository> logger, ApplicationContext context)
        {
            this.logger = logger;
            this.context = context;
        }
        
        public bool Any(long id)
        {
            return context.Users.Any(x => x.Id == id);
        }

        public User? Get(long id)
        {
            return context.Users.FirstOrDefault(x => x.Id == id);
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

        public long Create(User user)
        {
            logger.LogInformation("CreateUser");
            var entity = context.Users.Add(user).Entity;

            context.SaveChanges();

            return user.Id;
        }

        public void Update(User newUser)
        {
            var dbUser = Get(newUser.Id);

            if (dbUser == null) { throw new ArgumentNullException($"Can't find user with Id {newUser.Id}"); }

            dbUser.FirstName = newUser.FirstName; 
            dbUser.LastName = newUser.LastName;
            dbUser.IsActive = newUser.IsActive;
            dbUser.Source = newUser.Source ?? dbUser.Source;
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
