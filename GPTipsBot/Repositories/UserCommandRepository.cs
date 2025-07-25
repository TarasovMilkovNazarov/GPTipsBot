using GPTipsBot.Db;
using GPTipsBot.Dtos;
using GPTipsBot.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace GPTipsBot.Repositories
{
    public class UserCommandRepository(
        ApplicationContext context,
        ILogger<UserCommandRepository> logger,
        IMemoryCache memoryCache)
        : GenericRepository<UserCommand>(context)
    {
        private readonly ApplicationContext _context = context;
        private readonly ILogger<UserCommandRepository> _logger = logger;

        private readonly MemoryCacheEntryOptions _cacheOptions = new()
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1),
            SlidingExpiration = TimeSpan.FromMinutes(20),
        };
        private const string CacheKeyPrefix = "UserCommands_{0}_{1}";

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

            var cacheKey = string.Format(CacheKeyPrefix, entity.UserId, entity.ChatId);
            memoryCache.Set(cacheKey, entity, _cacheOptions);

            return entity;
        }

        public async Task<UserCommand?> GetLastAsync(UserChatKey userChatKey)
        {
            var cacheKey = string.Format(CacheKeyPrefix, userChatKey.Id, userChatKey.ChatId);

            if (memoryCache.TryGetValue(cacheKey, out UserCommand? cachedUserCommand))
            {
                return cachedUserCommand;
            }

            var command = await _context.UserCommands.AsNoTracking().Where(c => c.UserId == userChatKey.Id &&
                                                                         c.ChatId == userChatKey.ChatId)
                .OrderByDescending(c => c.CreatedAt).FirstOrDefaultAsync();

            memoryCache.Set(cacheKey, command, _cacheOptions);

            return command;
        }
    }
}
