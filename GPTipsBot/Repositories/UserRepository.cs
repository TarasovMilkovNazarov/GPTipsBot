using GPTipsBot.Db;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using User = GPTipsBot.Models.User;

namespace GPTipsBot.Repositories
{
    public class UserRepository
    {
        public const string CacheKeyPrefix = "user_";
        private readonly ILogger<UserRepository> _logger;
        private readonly ApplicationContext _context;
        private readonly IMemoryCache _memoryCache;

        public UserRepository(ILogger<UserRepository> logger, ApplicationContext context, IMemoryCache memoryCache)
        {
            _logger = logger;
            _context = context;
            _memoryCache = memoryCache;
        }
        
        public bool Any(long id)
        {
            return _context.Users.Any(x => x.Id == id);
        }

        public User? Get(long id)
        {
            return _context.Users
                .Include(u => u.Wallet)
                .SingleOrDefault(x => x.Id == id);
                // .AsNoTracking()
                // .Find(id);
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

        public async Task<long> Create(User user)
        {
            _logger.LogInformation("CreateUser");
            var entity = _context.Users.Add(user).Entity;
            await _context.SaveChangesAsync();

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

        public long GetActiveUsersCount()
        {
            return _context.Users.AsNoTracking().Count(x => x.IsActive);
        }

        public async Task<bool> SoftlyRemoveUser(long telegramId)
        {
            var user = Get(telegramId);
            if (user == null) return false;

            user.IsActive = false;
            // todo добавить UpdateAt поле для подсчета удаливших бота юзеров
            await _context.SaveChangesAsync();

            _memoryCache.Remove(CacheKeyPrefix+telegramId);
            return true;
        }
    }
}
