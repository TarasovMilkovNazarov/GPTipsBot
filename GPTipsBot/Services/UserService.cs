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
        private readonly WalletRepository _walletRepository;
        private readonly IMemoryCache _memoryCache;
        private readonly ApplicationContext _context;
        private readonly MemoryCacheEntryOptions _cacheOptions;
        private event EventHandler<User> UserCreated;
        private static long? activeUserCount;

        public UserService(ITelegramBotClient botClient, UserRepository userRepository,
            WalletRepository walletRepository, IMemoryCache memoryCache, ApplicationContext context)
        {
            _botClient = botClient;
            _userRepository = userRepository;
            _walletRepository = walletRepository;
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
                Stars = user.Wallet?.Balance ?? 0.0,
                Images = user.FreeImageGenerations,
                ImageTexts = user.FreeImageTextRecognitions,
                GptRequests = user.FreeGptRequests
            };

            return profile;
        }

        public async Task<bool> PayForGpt(long userId)
        {
            const double payment = 0.05;
            var user = _userRepository.Get(userId);
            Guard.Against.Null(user);

            if (user.FreeGptRequests > 0)
            {
                user.FreeGptRequests -= 1;
                _userRepository.Update(user);

                return true;
            }

            var wallet = user.Wallet;

            if (wallet is null || wallet.Balance < payment)
            {
                return false;
            }

            user.Wallet!.Balance -= payment;
            return true;
        }

        public async Task<bool> PayForImageAsync(long userId)
        {
            const int payment = 1;
            var user = _userRepository.Get(userId);
            Guard.Against.Null(user);

            if (user.FreeImageGenerations > 0)
            {
                user.FreeImageGenerations -= 1;
                _userRepository.Update(user);

                return true;
            }

            if (user.Wallet == null || user.Wallet?.Balance < payment)
            {
                return false;
            }

            user.Wallet!.Balance -= payment;

            return true;
        }

        public async Task<bool> PayForTextRecognitions(long userId)
        {
            const double payment = 0.5;
            var user = _userRepository.Get(userId);
            Guard.Against.Null(user);

            if (user.FreeImageTextRecognitions > 0)
            {
                user.FreeImageGenerations -= 1;
                _userRepository.Update(user);

                return true;
            }

            if (user.Wallet == null || user.Wallet?.Balance < payment)
            {
                return false;
            }

            user.Wallet!.Balance -= payment;
            return true;
        }

        public async Task CreateUpdateUser(User user)
        {
            var cacheKey = $"User_{user.Id}";

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
                await _userRepository.Create(user);
                UserCreated?.Invoke(this, user);
            }

            _memoryCache.Set(cacheKey, user, _cacheOptions);
        }

        private void UserCreatedEventHandler(object? sender, User user)
        {
            var fullName = user.FirstName;
            fullName += user.LastName == null ? "" : $" {user.LastName}";

            activeUserCount ??= _userRepository.GetActiveUsersCount();

            activeUserCount++;

            var message = "#newUser" + Environment.NewLine + $"{fullName} with telegramId={user.Id} created";
            message += Environment.NewLine + $"Total count: {activeUserCount}";

            foreach (var adminId in AppConfig.AdminIds)
            {
                _botClient.SendTextMessageAsync(adminId, message);   
            }
        }
    }
}
