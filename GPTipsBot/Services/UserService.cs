using Ardalis.GuardClauses;
using GPTipsBot.Db;
using GPTipsBot.Models;
using GPTipsBot.Repositories;
using Microsoft.Extensions.Caching.Memory;
using Telegram.Bot;

namespace GPTipsBot.Services
{
    public class UserService
    {
        private readonly ITelegramBotClient _botClient;
        private readonly UserRepository _userRepository;
        private readonly IMemoryCache _memoryCache;
        private readonly ApplicationContext _context;
        private readonly MemoryCacheEntryOptions _cacheOptions;
        public event EventHandler<User> UserCreated;
        public static long? ActiveUserCount;

        public UserService(ITelegramBotClient botClient, UserRepository userRepository, IMemoryCache memoryCache,
            ApplicationContext context)
        {
            _botClient = botClient;
            _userRepository = userRepository;
            UserCreated += UserCreatedEventHandler;

            _memoryCache = memoryCache;
            _context = context;

            _cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1),
                SlidingExpiration = TimeSpan.FromMinutes(20),
            };
        }

        public async Task<UserProfileDto> GetUserProfile(long userId)
        {
            var user = _userRepository.Get(userId);
            Guard.Against.Null(user);

            var profile = new UserProfileDto()
            {
                FirstName = user.FirstName,
                LastName = user.LastName,
                Stars = user.Wallet?.Amount ?? 0,
                Images = user.FreeImageGenerations,
                ImageTexts = user.FreeImageTextRecognitions,
            };

            return profile;
        }

        public async Task<bool> DecreaseFreeImageGenerationsAsync(long userId)
        {
            var user = _userRepository.Get(userId);
            Guard.Against.Null(user);

            user.FreeImageGenerations -= 1;
            _userRepository.Update(user);

            return true;
        }

        public async Task<bool> DecreaseFreeImageRecognitionsAsync(long userId)
        {
            var user = _userRepository.Get(userId);
            Guard.Against.Null(user);

            user.FreeImageTextRecognitions -= 1;
            _userRepository.Update(user);

            return true;
        }

        public void CreateUpdateUser(User user)
        {
            string cacheKey = $"User_{user.Id}";

            if (_memoryCache.TryGetValue(cacheKey, out User _))
            {
                return;
            }

            var isExists = _userRepository.Any(user.Id);
            if (isExists)
            {
                _userRepository.Update(user);
            }
            else
            {
                _userRepository.Create(user);
                UserCreated?.Invoke(this, user);
            }

            _memoryCache.Set(cacheKey, user, _cacheOptions);
        }

        public void UserCreatedEventHandler(object sender, User user)
        {
            var fullName = user.FirstName;
            fullName += user.LastName == null ? "" : $" {user.LastName}";

            if (ActiveUserCount == null)
            {
                ActiveUserCount = _userRepository.GetActiveUsersCount();
            }
            else
            {
                ActiveUserCount++;
            }

            var message = "#newUser" + Environment.NewLine + $"{fullName} with telegramId={user.Id} created";
            message += Environment.NewLine + $"Total count: {ActiveUserCount}";

            foreach (var adminId in AppConfig.AdminIds)
            {
                _botClient.SendTextMessageAsync(adminId, message);   
            }
        }
    }
}
