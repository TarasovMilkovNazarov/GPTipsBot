using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using GPTipsBot.Config;
using GPTipsBot.Db;
using GPTipsBot.Models;
using GPTipsBot.Repositories;
using GPTipsBot.Services;
using GPTipsBot.Services.Email;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace GPTipsBot.Web;

public class WebUserService(
    UserService userService,
    UserRepository userRepository,
    BotSettingsRepository botSettingsRepository,
    ApplicationContext context,
    SmtpEmailSender emailSender,
    ILogger<WebUserService> logger)
{
    /// <summary>Guest free quotas (reduced vs Telegram newbie).</summary>
    public const int GuestFreeGpt = 5;
    public const int GuestFreeImages = 3;
    public const int GuestFreeOcr = 3;
    public const int GuestFreeAnimations = 1;

    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public async Task<(User User, bool IsGuest)> EnsureGuestAsync(string? language = "en")
    {
        var guestId = await AllocateGuestUserIdAsync();
        var user = new User
        {
            Id = guestId,
            FirstName = "Guest",
            Source = WebAuthConstants.GuestSource,
            CreatedAt = DateTimeOffset.UtcNow,
            IsActive = true,
            FreeGptRequests = GuestFreeGpt,
            FreeImageGenerations = GuestFreeImages,
            FreeImageTextRecognitions = GuestFreeOcr,
            FreePhotoAnimations = GuestFreeAnimations,
            FreeSummaryRequests = 0,
        };

        await userService.CreateUpdateUser(user);
        EnsureSettings(guestId, language ?? "en");
        await context.SaveChangesAsync();

        return (userRepository.Get(guestId)!, true);
    }

    public async Task<(User User, bool IsGuest)> EnsureTelegramUserAsync(
        TelegramLoginPayload payload,
        string? language = "en")
    {
        var user = new User
        {
            Id = payload.Id,
            FirstName = payload.FirstName,
            LastName = payload.LastName,
            Source = WebAuthConstants.TelegramSource,
            CreatedAt = DateTimeOffset.UtcNow,
            IsActive = true,
        };

        await userService.CreateUpdateUser(user);
        EnsureSettings(payload.Id, language ?? "en");
        await context.SaveChangesAsync();

        return (userRepository.Get(payload.Id)!, false);
    }

    public async Task<(User User, string? DevCode)> RegisterEmailAsync(
        string email,
        string password,
        string? firstName,
        string? language,
        CancellationToken cancellationToken)
    {
        var normalized = NormalizeEmail(email);
        if (normalized is null)
        {
            throw new ArgumentException("Invalid email");
        }

        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
        {
            throw new ArgumentException("Password must be at least 6 characters");
        }

        var existing = await context.Users.FirstOrDefaultAsync(u => u.Email == normalized, cancellationToken);
        if (existing is { EmailConfirmed: true })
        {
            throw new InvalidOperationException("Email already registered");
        }

        var code = GenerateConfirmCode();
        var expires = DateTimeOffset.UtcNow.AddMinutes(30);
        var hash = PasswordHasher.Hash(password);
        var displayName = string.IsNullOrWhiteSpace(firstName)
            ? normalized.Split('@')[0]
            : firstName.Trim();

        User user;
        if (existing is null)
        {
            var id = await AllocateEmailUserIdAsync();
            user = new User
            {
                Id = id,
                FirstName = displayName,
                Source = WebAuthConstants.EmailSource,
                CreatedAt = DateTimeOffset.UtcNow,
                IsActive = true,
                Email = normalized,
                PasswordHash = hash,
                EmailConfirmed = false,
                EmailConfirmCode = code,
                EmailConfirmExpiresAt = expires,
                FreeGptRequests = PaymentConfig.NewbieFreeChatGptRequests,
                FreeImageGenerations = PaymentConfig.NewbieFreeImageGenerations,
                FreeImageTextRecognitions = PaymentConfig.NewbieFreeTextRecognitions,
                FreePhotoAnimations = PaymentConfig.NewbieFreePhotoAnimations,
                FreeSummaryRequests = PaymentConfig.NewbieFreeSummaries,
            };
            await userService.CreateUpdateUser(user);
            EnsureSettings(id, language ?? "en");
        }
        else
        {
            existing.FirstName = displayName;
            existing.PasswordHash = hash;
            existing.EmailConfirmCode = code;
            existing.EmailConfirmExpiresAt = expires;
            existing.Source = WebAuthConstants.EmailSource;
            existing.IsActive = true;
            user = existing;
        }

        await context.SaveChangesAsync(cancellationToken);
        var devCode = await SendConfirmCodeAsync(normalized, code, cancellationToken);
        return (userRepository.Get(user.Id)!, devCode);
    }

    public async Task<(User User, string? DevCode)> ResendEmailCodeAsync(
        string email,
        CancellationToken cancellationToken)
    {
        var normalized = NormalizeEmail(email)
            ?? throw new ArgumentException("Invalid email");
        var user = await context.Users.FirstOrDefaultAsync(u => u.Email == normalized, cancellationToken)
            ?? throw new InvalidOperationException("Account not found");

        if (user.EmailConfirmed)
        {
            throw new InvalidOperationException("Email already confirmed");
        }

        var code = GenerateConfirmCode();
        user.EmailConfirmCode = code;
        user.EmailConfirmExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30);
        await context.SaveChangesAsync(cancellationToken);
        var devCode = await SendConfirmCodeAsync(normalized, code, cancellationToken);
        return (user, devCode);
    }

    public async Task<User> ConfirmEmailAsync(string email, string code, CancellationToken cancellationToken)
    {
        var normalized = NormalizeEmail(email)
            ?? throw new ArgumentException("Invalid email");
        var user = await context.Users.FirstOrDefaultAsync(u => u.Email == normalized, cancellationToken)
            ?? throw new InvalidOperationException("Account not found");

        if (user.EmailConfirmed)
        {
            return user;
        }

        if (string.IsNullOrWhiteSpace(user.EmailConfirmCode) ||
            user.EmailConfirmExpiresAt is null ||
            user.EmailConfirmExpiresAt < DateTimeOffset.UtcNow ||
            !string.Equals(user.EmailConfirmCode.Trim(), code.Trim(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Invalid or expired confirmation code");
        }

        user.EmailConfirmed = true;
        user.EmailConfirmCode = null;
        user.EmailConfirmExpiresAt = null;
        await context.SaveChangesAsync(cancellationToken);
        return userRepository.Get(user.Id)!;
    }

    public async Task<User> LoginEmailAsync(string email, string password, CancellationToken cancellationToken)
    {
        var normalized = NormalizeEmail(email)
            ?? throw new ArgumentException("Invalid email");
        var user = await context.Users.FirstOrDefaultAsync(u => u.Email == normalized, cancellationToken)
            ?? throw new InvalidOperationException("Invalid email or password");

        if (!PasswordHasher.Verify(password, user.PasswordHash))
        {
            throw new InvalidOperationException("Invalid email or password");
        }

        if (!user.EmailConfirmed)
        {
            throw new InvalidOperationException("Email is not confirmed");
        }

        if (!user.IsActive)
        {
            throw new InvalidOperationException("Account is disabled");
        }

        return userRepository.Get(user.Id)!;
    }

    public static long? TryGetUserId(HttpContext httpContext)
    {
        var claim = httpContext.User.FindFirstValue(WebAuthConstants.UserIdClaim)
                    ?? httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(claim, out var id) ? id : null;
    }

    public static bool IsGuest(HttpContext httpContext) =>
        httpContext.User.FindFirstValue(WebAuthConstants.IsGuestClaim) == "1";

    public async Task SignInAsync(HttpContext httpContext, long userId, bool isGuest)
    {
        var claims = new List<Claim>
        {
            new(WebAuthConstants.UserIdClaim, userId.ToString()),
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(WebAuthConstants.IsGuestClaim, isGuest ? "1" : "0"),
        };

        var identity = new ClaimsIdentity(claims, WebAuthConstants.Scheme);
        await httpContext.SignInAsync(
            WebAuthConstants.Scheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30),
            });
    }

    public Task SignOutAsync(HttpContext httpContext) =>
        httpContext.SignOutAsync(WebAuthConstants.Scheme);

    public async Task RecordLoginAsync(long userId, AuthProvider provider)
    {
        context.AuthLoginEvents.Add(new AuthLoginEvent
        {
            UserId = userId,
            Provider = provider,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await context.SaveChangesAsync();
    }

    private async Task<string?> SendConfirmCodeAsync(
        string email,
        string code,
        CancellationToken cancellationToken)
    {
        var subject = "GPTipsBot confirmation code";
        var body = $"Your confirmation code: {code}\n\nThe code is valid for 30 minutes.";
        if (!SmtpConfig.IsEnabled)
        {
            logger.LogWarning("SMTP disabled — confirmation code for {Email}: {Code}", email, code);
            return code;
        }

        await emailSender.SendAsync(email, subject, body, cancellationToken);
        return null;
    }

    private void EnsureSettings(long userId, string language)
    {
        if (botSettingsRepository.Get(userId) == null)
        {
            botSettingsRepository.Create(userId, language, GptModelCatalog.DefaultModelId);
        }
    }

    private async Task<long> AllocateGuestUserIdAsync()
    {
        for (var i = 0; i < 8; i++)
        {
            var candidate = -Math.Abs(Random.Shared.NextInt64(1, long.MaxValue / 2));
            if (!userRepository.Any(candidate))
            {
                return candidate;
            }
        }

        var fallback = -DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        while (userRepository.Any(fallback))
        {
            fallback--;
        }

        await Task.CompletedTask;
        return fallback;
    }

    private async Task<long> AllocateEmailUserIdAsync()
    {
        for (var i = 0; i < 12; i++)
        {
            var candidate = WebAuthConstants.EmailIdBase + Math.Abs(Random.Shared.NextInt64(1, 900_000_000_000L));
            if (!userRepository.Any(candidate))
            {
                return candidate;
            }
        }

        var fallback = WebAuthConstants.EmailIdBase + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        while (userRepository.Any(fallback))
        {
            fallback++;
        }

        await Task.CompletedTask;
        return fallback;
    }

    private static string GenerateConfirmCode() =>
        RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

    private static string? NormalizeEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var normalized = email.Trim().ToLowerInvariant();
        return EmailRegex.IsMatch(normalized) ? normalized : null;
    }
}
