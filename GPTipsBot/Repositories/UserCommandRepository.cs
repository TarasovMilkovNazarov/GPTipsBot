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
        private readonly ApplicationContext context;
        private readonly ILogger<UserCommandRepository> logger;
        private readonly IMemoryCache memoryCache;
        private readonly MemoryCacheEntryOptions _cacheOptions;
        private const string CacheKeyPrefix = "UserCommands_{0}_{1}";

        public UserCommandRepository(ApplicationContext context, ILogger<UserCommandRepository> logger,
            IMemoryCache memoryCache) : base(context)
        {
            this.context = context;
            this.logger = logger;
            this.memoryCache = memoryCache;

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
            context.UserCommands.Add(entity);
            await context.SaveChangesAsync();

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

            var command = await context.UserCommands.AsNoTracking().Where(c => c.UserId == userChatKey.Id &&
                                                                         c.ChatId == userChatKey.ChatId)
                .OrderByDescending(c => c.CreatedAt).FirstOrDefaultAsync();

            memoryCache.Set(cacheKey, command, _cacheOptions);

            return command;
        }
    }
}
