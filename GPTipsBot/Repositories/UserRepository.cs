using GPTipsBot.Db;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using User = GPTipsBot.Models.User;

namespace GPTipsBot.Repositories
{
    public class UserRepository
    {
        private readonly ILogger<UserRepository> _logger;
        private readonly ApplicationContext _context;
        public Guid Guid { get; } = Guid.NewGuid();

        public UserRepository(ILogger<UserRepository> logger, ApplicationContext context)
        {
            _logger = logger;
            _context = context;
        }
        
        public bool Any(long id)
        {
            return _context.Users.Any(x => x.Id == id);
        }

        public User? Get(long id)
        {
            return _context.Users
                // .AsNoTracking()
                .Find(id);
        }

        public void Delete(long id)
        {
            var user = _context.Users.FirstOrDefault(x => x.Id == id);

            if (user == null)
            {
                throw new Exception($"User id={id} not found");
            }

            _context.Users.Remove(user);
        }

        public long Create(User user)
        {
            _logger.LogInformation("CreateUser");
            var entity = _context.Users.Add(user).Entity;

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
            return _context.Users.AsNoTracking().ToList();
        }

        public long GetActiveUsersCount()
        {
            return _context.Users.AsNoTracking().Where(x => x.IsActive).Count();
        }

        public long SoftlyRemoveUser(long telegramId)
        {
            return _context.Users.Where(x => x.Id == telegramId).ExecuteUpdate(x => x.SetProperty(y => y.IsActive, false));
        }
    }
}
