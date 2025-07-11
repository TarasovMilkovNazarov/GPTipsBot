using GPTipsBot.Db;
using GPTipsBot.Dtos;
using GPTipsBot.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace GPTipsBot.Repositories
{
    public class UserCommandRepository: GenericRepository<UserCommand>
    {
        private readonly ApplicationContext _context;
        private readonly ILogger<UserCommandRepository> _logger;
        private readonly IMemoryCache _memoryCache;
        private readonly MemoryCacheEntryOptions _cacheOptions;
        private const string CacheKeyPrefix = "UserCommands_{0}_{1}";

        public UserCommandRepository(ApplicationContext context, ILogger<UserCommandRepository> logger,
            IMemoryCache memoryCache) : base(context)
        {
            _context = context;
            _logger = logger;
            _memoryCache = memoryCache;

            _cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1),
                SlidingExpiration = TimeSpan.FromMinutes(20),
            };
        }

        public async Task<UserCommand> AddAsync(UserChatKey userChatKey, CommandType commandType)
        {
            var entity = new UserCommand
            {
                UserId = userChatKey.Id,
                ChatId = userChatKey.ChatId,
                Type = commandType,
                CreatedAt = DateTime.UtcNow
            };
            _context.UserCommands.Add(entity);
            await _context.SaveChangesAsync();

            var cacheKey = string.Format(CacheKeyPrefix, entity.UserId, entity.ChatId);
            _memoryCache.Set(cacheKey, entity, _cacheOptions);

            return entity;
        }

        public async Task<UserCommand?> GetLastAsync(UserChatKey userChatKey)
        {
            var cacheKey = string.Format(CacheKeyPrefix, userChatKey.Id, userChatKey.ChatId);

            if (_memoryCache.TryGetValue(cacheKey, out UserCommand? cachedUserCommand))
            {
                return cachedUserCommand;
            }

            var command = await _context.UserCommands.AsNoTracking().Where(c => c.UserId == userChatKey.Id &&
                                                                         c.ChatId == userChatKey.ChatId)
                .OrderByDescending(c => c.CreatedAt).FirstOrDefaultAsync();

            _memoryCache.Set(cacheKey, command, _cacheOptions);

            return command;
        }
    }
}
