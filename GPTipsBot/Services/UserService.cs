using GPTipsBot.Models;
using GPTipsBot.Repositories;
using Microsoft.Extensions.Caching.Memory;
using Telegram.Bot;

namespace GPTipsBot.Services
{
    public class UserService
    {
        private readonly ITelegramBotClient botClient;
        private readonly UserRepository userRepository;
        private readonly IMemoryCache _memoryCache;
        private readonly MemoryCacheEntryOptions _cacheOptions;
        public event EventHandler<User> UserCreated;
        public static long? activeUserCount;

        public UserService(ITelegramBotClient botClient, UserRepository userRepository, IMemoryCache memoryCache)
        {
            this.botClient = botClient;
            this.userRepository = userRepository;
            UserCreated += UserCreatedEventHandler;

            _memoryCache = memoryCache;

            _cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1),
                SlidingExpiration = TimeSpan.FromMinutes(20),
            };
        }

        public void CreateUpdateUser(User user)
        {
            string cacheKey = $"User_{user.Id}";

            if (_memoryCache.TryGetValue(cacheKey, out User _))
            {
                return;
            }

            var isExists = userRepository.Any(user.Id);
            if (isExists)
            {
                userRepository.Update(user);
            }
            else
            {
                userRepository.Create(user);
                UserCreated?.Invoke(this, user);
            }

            _memoryCache.Set(cacheKey, user, _cacheOptions);
        }

        public void UserCreatedEventHandler(object sender, User user)
        {
            var fullName = user.FirstName;
            fullName += user.LastName == null ? "" : $" {user.LastName}";

            if (activeUserCount == null)
            {
                activeUserCount = userRepository.GetActiveUsersCount();
            }
            else
            {
                activeUserCount++;
            }

            var message = "#newUser" + Environment.NewLine + $"{fullName} with telegramId={user.Id} created";
            message += Environment.NewLine + $"Total count: {activeUserCount}";

            foreach (var adminId in AppConfig.AdminIds)
            {
                botClient.SendTextMessageAsync(adminId, message);   
            }
        }
    }
}
