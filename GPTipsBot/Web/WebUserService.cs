using System.Security.Claims;
using GPTipsBot.Config;
using GPTipsBot.Db;
using GPTipsBot.Models;
using GPTipsBot.Repositories;
using GPTipsBot.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

namespace GPTipsBot.Web;

public class WebUserService(
    UserService userService,
    UserRepository userRepository,
    BotSettingsRepository botSettingsRepository,
    ApplicationContext context)
{
    /// <summary>Guest free quotas (reduced vs Telegram newbie).</summary>
    public const int GuestFreeGpt = 5;
    public const int GuestFreeImages = 3;
    public const int GuestFreeOcr = 3;
    public const int GuestFreeAnimations = 1;

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

        // Extremely unlikely; fall back to timestamp-based id.
        var fallback = -DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        while (userRepository.Any(fallback))
        {
            fallback--;
        }

        await Task.CompletedTask;
        return fallback;
    }
}
