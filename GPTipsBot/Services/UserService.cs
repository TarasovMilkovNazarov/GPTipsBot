using Ardalis.GuardClauses;
using GPTipsBot.Config;
using GPTipsBot.Db;
using GPTipsBot.Models;
using GPTipsBot.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Npgsql;
using Telegram.Bot;

namespace GPTipsBot.Services
{
    public partial class UserService
    {
        private readonly ITelegramBotClient _botClient;
        private readonly UserRepository _userRepository;
        private readonly WalletRepository _walletRepository;
        private readonly BotSettingsRepository _botSettingsRepository;
        private readonly IMemoryCache _memoryCache;
        private readonly ApplicationContext _context;
        private readonly MemoryCacheEntryOptions _cacheOptions;
        private event EventHandler<User> UserCreated;
        private static long? activeUserCount;

        public UserService(ITelegramBotClient botClient, UserRepository userRepository,
            WalletRepository walletRepository, BotSettingsRepository botSettingsRepository,
            IMemoryCache memoryCache, ApplicationContext context)
        {
            _botClient = botClient;
            _userRepository = userRepository;
            _walletRepository = walletRepository;
            _botSettingsRepository = botSettingsRepository;
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

            var model = GetPreferredGptModel(userId);

            var profile = new UserProfileDto()
            {
                FirstName = user.FirstName,
                LastName = user.LastName,
                Stars = user.Wallet?.Balance ?? 0.0,
                Images = user.FreeImageGenerations,
                ImageTexts = user.FreeImageTextRecognitions,
                GptRequests = user.FreeGptRequests,
                PhotoAnimations = user.FreePhotoAnimations,
                Summaries = user.FreeSummaryRequests,
                GptModelId = model.Id,
                GptModelDisplayName = model.DisplayName,
            };

            return profile;
        }

        public GptModelOption GetPreferredGptModel(long userId) =>
            GptModelCatalog.Resolve(_botSettingsRepository.Get(userId)?.PreferredGptModel);

        public Task<PaymentHold?> TryReserveGptAsync(long userId) =>
            TryReserveGptAsync(userId, GetPreferredGptModel(userId));

        public Task<PaymentHold?> TryReserveGptAsync(long userId, GptModelOption model) =>
            TryReserveAsync(userId, PaidFeature.Gpt, model.StarsCost, allowFreeQuota: model.AllowFreeQuota);

        public Task<PaymentHold?> TryReserveImageAsync(long userId) =>
            TryReserveAsync(userId, PaidFeature.Image, PaymentConfig.Image);

        public Task<PaymentHold?> TryReserveTextRecognitionAsync(long userId) =>
            TryReserveAsync(userId, PaidFeature.TextRecognition, PaymentConfig.Image);

        public Task<PaymentHold?> TryReserveAnimationAsync(long userId) =>
            TryReserveAsync(userId, PaidFeature.Animation, PaymentConfig.Animation);

        public Task<PaymentHold?> TryReserveSummaryAsync(long userId) =>
            TryReserveAsync(userId, PaidFeature.Summary, PaymentConfig.Summary);

        public Task<PaymentHold?> TryReserveGptImageAsync(long userId, double starsCost) =>
            TryReserveAsync(userId, PaidFeature.GptImage, starsCost, allowFreeQuota: false);

        public async Task ConfirmAsync(long holdId)
        {
            await _context.PaymentHolds
                .Where(h => h.Id == holdId && h.Status == PaymentHoldStatus.Held)
                .ExecuteUpdateAsync(s => s.SetProperty(h => h.Status, PaymentHoldStatus.Confirmed));
        }

        public async Task ReleaseAsync(long holdId)
        {
            await using var tx = await _context.Database.BeginTransactionAsync();

            var claimed = await _context.PaymentHolds
                .Where(h => h.Id == holdId && h.Status == PaymentHoldStatus.Held)
                .ExecuteUpdateAsync(s => s.SetProperty(h => h.Status, PaymentHoldStatus.Released));

            if (claimed != 1)
            {
                await tx.RollbackAsync();
                return;
            }

            var hold = await _context.PaymentHolds.AsNoTracking()
                .FirstAsync(h => h.Id == holdId);

            if (hold.UsedFreeQuota)
            {
                await RestoreFreeQuotaAsync(hold.UserId, hold.Feature);
            }
            else if (hold.WalletAmount > 0)
            {
                await _context.Wallets
                    .Where(w => w.UserId == hold.UserId)
                    .ExecuteUpdateAsync(s => s.SetProperty(w => w.Balance, w => w.Balance + hold.WalletAmount));
            }

            await tx.CommitAsync();
        }

        public async Task ReleaseExpiredHoldsAsync(CancellationToken cancellationToken = default)
        {
            var cutoff = DateTime.UtcNow - PaymentConfig.PaymentHoldTtl;
            var expiredIds = await _context.PaymentHolds.AsNoTracking()
                .Where(h => h.Status == PaymentHoldStatus.Held && h.CreatedAt < cutoff)
                .Select(h => h.Id)
                .ToListAsync(cancellationToken);

            foreach (var holdId in expiredIds)
            {
                await ReleaseAsync(holdId);
            }
        }

        private async Task<PaymentHold?> TryReserveAsync(
            long userId,
            PaidFeature feature,
            double walletPrice,
            bool allowFreeQuota = true)
        {
            await using var tx = await _context.Database.BeginTransactionAsync();

            if (allowFreeQuota)
            {
                var freeUpdated = await DecrementFreeQuotaAsync(userId, feature);
                if (freeUpdated == 1)
                {
                    var hold = NewHold(userId, feature, usedFreeQuota: true, walletAmount: 0);
                    _context.PaymentHolds.Add(hold);
                    await _context.SaveChangesAsync();
                    await tx.CommitAsync();
                    return hold;
                }
            }

            var walletUpdated = await _context.Wallets
                .Where(w => w.UserId == userId && w.Balance >= walletPrice)
                .ExecuteUpdateAsync(s => s.SetProperty(w => w.Balance, w => w.Balance - walletPrice));

            if (walletUpdated == 1)
            {
                var hold = NewHold(userId, feature, usedFreeQuota: false, walletAmount: walletPrice);
                _context.PaymentHolds.Add(hold);
                await _context.SaveChangesAsync();
                await tx.CommitAsync();
                return hold;
            }

            await tx.RollbackAsync();
            return null;
        }

        private async Task<int> DecrementFreeQuotaAsync(long userId, PaidFeature feature) => feature switch
        {
            PaidFeature.Gpt => await _context.Users
                .Where(u => u.Id == userId && u.FreeGptRequests > 0)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.FreeGptRequests, u => u.FreeGptRequests - 1)),
            PaidFeature.Image => await _context.Users
                .Where(u => u.Id == userId && u.FreeImageGenerations > 0)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.FreeImageGenerations, u => u.FreeImageGenerations - 1)),
            PaidFeature.TextRecognition => await _context.Users
                .Where(u => u.Id == userId && u.FreeImageTextRecognitions > 0)
                .ExecuteUpdateAsync(s =>
                    s.SetProperty(u => u.FreeImageTextRecognitions, u => u.FreeImageTextRecognitions - 1)),
            PaidFeature.Animation => await _context.Users
                .Where(u => u.Id == userId && u.FreePhotoAnimations > 0)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.FreePhotoAnimations, u => u.FreePhotoAnimations - 1)),
            PaidFeature.Summary => await _context.Users
                .Where(u => u.Id == userId && u.FreeSummaryRequests > 0)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.FreeSummaryRequests, u => u.FreeSummaryRequests - 1)),
            _ => 0,
        };

        private async Task RestoreFreeQuotaAsync(long userId, PaidFeature feature)
        {
            switch (feature)
            {
                case PaidFeature.Gpt:
                    await _context.Users.Where(u => u.Id == userId)
                        .ExecuteUpdateAsync(s => s.SetProperty(u => u.FreeGptRequests, u => u.FreeGptRequests + 1));
                    break;
                case PaidFeature.Image:
                    await _context.Users.Where(u => u.Id == userId)
                        .ExecuteUpdateAsync(s =>
                            s.SetProperty(u => u.FreeImageGenerations, u => u.FreeImageGenerations + 1));
                    break;
                case PaidFeature.TextRecognition:
                    await _context.Users.Where(u => u.Id == userId)
                        .ExecuteUpdateAsync(s =>
                            s.SetProperty(u => u.FreeImageTextRecognitions, u => u.FreeImageTextRecognitions + 1));
                    break;
                case PaidFeature.Animation:
                    await _context.Users.Where(u => u.Id == userId)
                        .ExecuteUpdateAsync(s =>
                            s.SetProperty(u => u.FreePhotoAnimations, u => u.FreePhotoAnimations + 1));
                    break;
                case PaidFeature.Summary:
                    await _context.Users.Where(u => u.Id == userId)
                        .ExecuteUpdateAsync(s =>
                            s.SetProperty(u => u.FreeSummaryRequests, u => u.FreeSummaryRequests + 1));
                    break;
            }
        }

        private static PaymentHold NewHold(long userId, PaidFeature feature, bool usedFreeQuota, double walletAmount) =>
            new()
            {
                UserId = userId,
                Feature = feature,
                UsedFreeQuota = usedFreeQuota,
                WalletAmount = walletAmount,
                Status = PaymentHoldStatus.Held,
                CreatedAt = DateTime.UtcNow,
            };

        public async Task CreateUpdateUser(User user)
        {
            var cacheKey = UserRepository.CacheKeyPrefix + user.Id;

            if (_memoryCache.TryGetValue(cacheKey, out User _))
            {
                return;
            }

            if (_userRepository.Any(user.Id))
            {
                await _userRepository.Update(user);
            }
            else
            {
                try
                {
                    await _userRepository.Create(user);
                    UserCreated?.Invoke(this, user);
                }
                catch (DbUpdateException ex) when (IsUniqueViolation(ex))
                {
                    // Concurrent create for the same Telegram id — treat as update.
                    _context.Entry(user).State = EntityState.Detached;
                    await _userRepository.Update(user);
                }
            }

            _memoryCache.Set(cacheKey, user, _cacheOptions);
        }

        private static bool IsUniqueViolation(DbUpdateException ex) =>
            ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

        private void UserCreatedEventHandler(object? sender, User user)
        {
            if (user.Source is Web.WebAuthConstants.GuestSource
                or Web.WebAuthConstants.TelegramSource
                or Web.WebAuthConstants.EmailSource)
            {
                return;
            }

            var fullName = user.FirstName;
            fullName += user.LastName == null ? "" : $" {user.LastName}";

            activeUserCount ??= _userRepository.GetActiveUsersCount();
            activeUserCount++;

            var message = $"#newUser_{DateTime.UtcNow:dd_MM_yyyy}"
                          + Environment.NewLine + $"{fullName} with telegramId={user.Id} created";
            message += Environment.NewLine + $"Total count: {activeUserCount}";

            foreach (var adminId in AppConfig.AdminIds)
            {
                _botClient.SendMessage(adminId, message).GetAwaiter().GetResult();
            }
        }
    }
}
