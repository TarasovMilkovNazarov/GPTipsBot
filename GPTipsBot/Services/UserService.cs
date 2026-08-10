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

        public User? GetById(long userId) => _userRepository.Get(userId);

        public User? GetByTelegramId(long telegramId) => _userRepository.GetByTelegramId(telegramId);

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
            if (user.TelegramId is long telegramId)
            {
                var byTelegram = _userRepository.GetByTelegramId(telegramId);
                if (byTelegram != null)
                {
                    user.Id = byTelegram.Id;
                    var cacheKeyExisting = UserRepository.CacheKeyPrefix + user.Id;
                    if (_memoryCache.TryGetValue(cacheKeyExisting, out User _))
                    {
                        return;
                    }

                    await _userRepository.Update(user);
                    CacheUser(user);
                    return;
                }

                // New pure Telegram user: keep Id = TelegramId (phase 1, no PK remap).
                if (user.Id == 0 || user.Id != telegramId)
                {
                    user.Id = telegramId;
                }

                user.TelegramId = telegramId;
            }

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
                    if (user.TelegramId is long tid)
                    {
                        var raced = _userRepository.GetByTelegramId(tid);
                        if (raced != null)
                        {
                            user.Id = raced.Id;
                        }
                    }

                    await _userRepository.Update(user);
                }
            }

            CacheUser(user);
        }

        private void CacheUser(User user)
        {
            _memoryCache.Set(UserRepository.CacheKeyPrefix + user.Id, user, _cacheOptions);
            if (user.TelegramId is long telegramId)
            {
                _memoryCache.Set(UserRepository.TelegramCacheKeyPrefix + telegramId, user, _cacheOptions);
            }
        }

        /// <summary>
        /// Merges loser into survivor (current session). Unique fields move to survivor; loser is deactivated.
        /// </summary>
        public async Task<User> MergeUsersAsync(long survivorId, long loserId)
        {
            if (survivorId == loserId)
            {
                return _userRepository.Get(survivorId)
                       ?? throw new InvalidOperationException("Survivor user not found");
            }

            await using var tx = await _context.Database.BeginTransactionAsync();

            var survivor = await _context.Users.Include(u => u.Wallet)
                .FirstOrDefaultAsync(u => u.Id == survivorId)
                ?? throw new InvalidOperationException("Survivor user not found");
            var loser = await _context.Users.Include(u => u.Wallet)
                .FirstOrDefaultAsync(u => u.Id == loserId)
                ?? throw new InvalidOperationException("Loser user not found");

            survivor.TelegramId ??= loser.TelegramId;
            if (survivor.Email is null && loser.Email is not null)
            {
                survivor.Email = loser.Email;
                survivor.PasswordHash = loser.PasswordHash;
                survivor.EmailConfirmed = loser.EmailConfirmed;
                survivor.EmailConfirmCode = loser.EmailConfirmCode;
                survivor.EmailConfirmExpiresAt = loser.EmailConfirmExpiresAt;
            }
            else if (survivor.Email is not null && loser.Email is not null
                     && !string.Equals(survivor.Email, loser.Email, StringComparison.OrdinalIgnoreCase)
                     && !survivor.EmailConfirmed && loser.EmailConfirmed)
            {
                survivor.Email = loser.Email;
                survivor.PasswordHash = loser.PasswordHash;
                survivor.EmailConfirmed = loser.EmailConfirmed;
                survivor.EmailConfirmCode = null;
                survivor.EmailConfirmExpiresAt = null;
            }

            survivor.FreeGptRequests += loser.FreeGptRequests;
            survivor.FreeImageGenerations += loser.FreeImageGenerations;
            survivor.FreeImageTextRecognitions += loser.FreeImageTextRecognitions;
            survivor.FreePhotoAnimations += loser.FreePhotoAnimations;
            survivor.FreeSummaryRequests += loser.FreeSummaryRequests;
            survivor.IsActive = true;

            var loserBalance = loser.Wallet?.Balance ?? 0;
            if (loserBalance > 0)
            {
                if (survivor.Wallet is null)
                {
                    survivor.Wallet = new Wallet
                    {
                        UserId = survivor.Id,
                        Balance = loserBalance,
                        Currency = Currency.Stars,
                        CreatedAt = DateTime.UtcNow,
                    };
                    _context.Wallets.Add(survivor.Wallet);
                }
                else
                {
                    survivor.Wallet.Balance += loserBalance;
                }

                if (loser.Wallet is not null)
                {
                    loser.Wallet.Balance = 0;
                }
            }

            // Clear unique fields on loser before reassignment so unique indexes stay valid.
            var loserTelegramId = loser.TelegramId;
            var loserEmail = loser.Email;
            loser.TelegramId = null;
            loser.Email = null;
            loser.PasswordHash = null;
            loser.EmailConfirmCode = null;
            loser.EmailConfirmExpiresAt = null;
            loser.EmailConfirmed = false;
            loser.IsActive = false;
            loser.FreeGptRequests = 0;
            loser.FreeImageGenerations = 0;
            loser.FreeImageTextRecognitions = 0;
            loser.FreePhotoAnimations = 0;
            loser.FreeSummaryRequests = 0;

            await _context.SaveChangesAsync();

            await _context.Messages
                .Where(m => m.UserId == loserId)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.UserId, survivorId));

            // Web chats use ChatId == UserId; remap those rows to survivor's web chat key.
            await _context.Messages
                .Where(m => m.ChatId == loserId)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.ChatId, survivorId));

            await _context.Invoices
                .Where(i => i.UserId == loserId)
                .ExecuteUpdateAsync(s => s.SetProperty(i => i.UserId, survivorId));

            await _context.PaymentHolds
                .Where(h => h.UserId == loserId)
                .ExecuteUpdateAsync(s => s.SetProperty(h => h.UserId, survivorId));

            await _context.UserCommands
                .Where(c => c.UserId == loserId)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.UserId, survivorId));

            await _context.AuthLoginEvents
                .Where(e => e.UserId == loserId)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.UserId, survivorId));

            var loserMetas = await _context.ConversationMetas
                .Where(m => m.UserId == loserId)
                .ToListAsync();
            foreach (var meta in loserMetas)
            {
                var existing = await _context.ConversationMetas
                    .FirstOrDefaultAsync(m => m.UserId == survivorId && m.ContextId == meta.ContextId);
                if (existing is null)
                {
                    _context.ConversationMetas.Add(new ConversationMeta
                    {
                        UserId = survivorId,
                        ContextId = meta.ContextId,
                        CustomTitle = meta.CustomTitle,
                        IsPinned = meta.IsPinned,
                        IsDeleted = meta.IsDeleted,
                    });
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(existing.CustomTitle))
                    {
                        existing.CustomTitle = meta.CustomTitle;
                    }

                    existing.IsPinned = existing.IsPinned || meta.IsPinned;
                    existing.IsDeleted = existing.IsDeleted && meta.IsDeleted;
                }

                _context.ConversationMetas.Remove(meta);
            }

            var loserSettings = await _context.BotSettings.FirstOrDefaultAsync(s => s.Id == loserId);
            if (loserSettings is not null)
            {
                _context.BotSettings.Remove(loserSettings);
            }

            await _context.SaveChangesAsync();
            await tx.CommitAsync();

            _userRepository.InvalidateCache(survivorId, survivor.TelegramId ?? loserTelegramId);
            _userRepository.InvalidateCache(loserId, loserTelegramId);

            return _userRepository.Get(survivorId)!;
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

            var telegramId = user.TelegramId ?? user.Id;
            var message = $"#newUser_{DateTime.UtcNow:dd_MM_yyyy}"
                          + Environment.NewLine + $"{fullName} with telegramId={telegramId} created";
            message += Environment.NewLine + $"Total count: {activeUserCount}";

            foreach (var adminId in AppConfig.AdminIds)
            {
                _botClient.SendMessage(adminId, message).GetAwaiter().GetResult();
            }
        }
    }
}
