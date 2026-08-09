using System.Security.Claims;
using GPTipsBot.Config;
using GPTipsBot.Db;
using GPTipsBot.Models;
using GPTipsBot.Repositories;
using GPTipsBot.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace GPTipsBot.Web;

public static class WebApiEndpoints
{
    public static void MapWebApi(this WebApplication app)
    {
        var api = app.MapGroup("/api");

        api.MapPost("/auth/guest", EnsureGuestAsync);
        api.MapPost("/auth/telegram", TelegramLoginAsync);
        api.MapPost("/auth/email/register", EmailRegisterAsync);
        api.MapPost("/auth/email/confirm", EmailConfirmAsync);
        api.MapPost("/auth/email/resend", EmailResendAsync);
        api.MapPost("/auth/email/login", EmailLoginAsync);
        api.MapPost("/auth/logout", LogoutAsync);
        api.MapGet("/me", GetMeAsync);
        api.MapGet("/models", GetModelsAsync);
        api.MapPut("/models/preferred", SetPreferredModelAsync);
        api.MapGet("/conversations", ListConversationsAsync);
        api.MapGet("/conversations/{contextId:long}", GetConversationAsync);
        api.MapPatch("/conversations/{contextId:long}", RenameConversationAsync);
        api.MapPost("/conversations/{contextId:long}/pin", PinConversationAsync);
        api.MapDelete("/conversations/{contextId:long}", DeleteConversationAsync);
        api.MapPost("/chat", ChatAsync);
        api.MapPost("/images/generate", GenerateImageAsync);
        api.MapPost("/ocr", OcrAsync);
        api.MapPost("/stt", SttAsync);
        api.MapGet("/presets/images", () => Results.Ok(ImagePresets.All));
        api.MapGet("/config/public", GetPublicConfigAsync);
        api.MapGet("/payments/packages", GetPaymentPackagesAsync);
        api.MapPost("/payments/yookassa", CreateYooKassaPaymentAsync);
        api.MapPost("/payments/{invoiceId:long}/sync", SyncPaymentAsync);
    }

    private static async Task<IResult> EnsureGuestAsync(
        HttpContext http,
        WebUserService webUsers,
        ApplicationContext db)
    {
        var existingId = WebUserService.TryGetUserId(http);
        if (existingId is not null && db.Users.Any(u => u.Id == existingId))
        {
            return Results.Ok(await BuildMeAsync(existingId.Value, webUsers, http));
        }

        var lang = http.Request.Headers.AcceptLanguage.ToString();
        var language = lang.StartsWith("ru", StringComparison.OrdinalIgnoreCase) ? "ru" : "en";
        var (user, isGuest) = await webUsers.EnsureGuestAsync(language);
        await webUsers.RecordLoginAsync(user.Id, AuthProvider.Guest);
        await webUsers.SignInAsync(http, user.Id, isGuest);
        return Results.Ok(await BuildMeAsync(user.Id, webUsers, http));
    }

    private static async Task<IResult> TelegramLoginAsync(
        HttpContext http,
        TelegramLoginRequest body,
        WebUserService webUsers,
        MessageRepository messages,
        ApplicationContext db)
    {
        var payload = new TelegramLoginPayload(
            body.Id,
            body.FirstName ?? "User",
            body.LastName,
            body.Username,
            body.PhotoUrl,
            body.AuthDate,
            body.Hash ?? "");

        if (!TelegramLoginValidator.TryValidate(payload, out var error))
        {
            return Results.BadRequest(new { message = error });
        }

        // Capture guest session before cookie is replaced by Telegram identity.
        var previousId = WebUserService.TryGetUserId(http);
        long? guestToMerge = null;
        if (previousId is < 0)
        {
            guestToMerge = previousId;
        }
        else if (body.PreviousGuestId is < 0)
        {
            guestToMerge = body.PreviousGuestId;
        }

        var lang = http.Request.Headers.AcceptLanguage.ToString();
        var language = lang.StartsWith("ru", StringComparison.OrdinalIgnoreCase) ? "ru" : "en";
        var (user, isGuest) = await webUsers.EnsureTelegramUserAsync(payload, language);

        if (guestToMerge is long guestId && guestId != user.Id)
        {
            var guestUser = db.Users.AsNoTracking().FirstOrDefault(u => u.Id == guestId);
            if (guestUser?.Source == WebAuthConstants.GuestSource)
            {
                await messages.TransferWebConversationsAsync(guestId, user.Id);
            }
        }

        await webUsers.RecordLoginAsync(user.Id, AuthProvider.Telegram);
        await webUsers.SignInAsync(http, user.Id, isGuest);
        return Results.Ok(await BuildMeAsync(user.Id, webUsers, http));
    }

    private static async Task<IResult> EmailRegisterAsync(
        HttpContext http,
        EmailRegisterRequest body,
        WebUserService webUsers)
    {
        try
        {
            var lang = http.Request.Headers.AcceptLanguage.ToString();
            var language = lang.StartsWith("ru", StringComparison.OrdinalIgnoreCase) ? "ru" : "en";
            var (user, devCode) = await webUsers.RegisterEmailAsync(
                body.Email ?? "",
                body.Password ?? "",
                body.FirstName,
                language,
                http.RequestAborted);

            return Results.Ok(new
            {
                needsConfirmation = true,
                email = user.Email,
                message = "Confirmation code sent",
                devCode,
            });
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static async Task<IResult> EmailConfirmAsync(
        HttpContext http,
        EmailConfirmRequest body,
        WebUserService webUsers,
        MessageRepository messages,
        ApplicationContext db)
    {
        try
        {
            var previousId = WebUserService.TryGetUserId(http);
            var user = await webUsers.ConfirmEmailAsync(body.Email ?? "", body.Code ?? "", http.RequestAborted);

            if (previousId is < 0)
            {
                var guestUser = db.Users.AsNoTracking().FirstOrDefault(u => u.Id == previousId);
                if (guestUser?.Source == WebAuthConstants.GuestSource)
                {
                    await messages.TransferWebConversationsAsync(previousId.Value, user.Id);
                }
            }
            else if (body.PreviousGuestId is < 0)
            {
                var guestUser = db.Users.AsNoTracking().FirstOrDefault(u => u.Id == body.PreviousGuestId);
                if (guestUser?.Source == WebAuthConstants.GuestSource)
                {
                    await messages.TransferWebConversationsAsync(body.PreviousGuestId.Value, user.Id);
                }
            }

            await webUsers.RecordLoginAsync(user.Id, AuthProvider.Email);
            await webUsers.SignInAsync(http, user.Id, isGuest: false);
            return Results.Ok(await BuildMeAsync(user.Id, webUsers, http));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static async Task<IResult> EmailResendAsync(
        HttpContext http,
        EmailResendRequest body,
        WebUserService webUsers)
    {
        try
        {
            var (_, devCode) = await webUsers.ResendEmailCodeAsync(body.Email ?? "", http.RequestAborted);
            return Results.Ok(new { ok = true, message = "Confirmation code sent", devCode });
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static async Task<IResult> EmailLoginAsync(
        HttpContext http,
        EmailLoginRequest body,
        WebUserService webUsers,
        MessageRepository messages,
        ApplicationContext db)
    {
        try
        {
            var previousId = WebUserService.TryGetUserId(http);
            var user = await webUsers.LoginEmailAsync(body.Email ?? "", body.Password ?? "", http.RequestAborted);

            long? guestToMerge = previousId is < 0 ? previousId : body.PreviousGuestId is < 0 ? body.PreviousGuestId : null;
            if (guestToMerge is long guestId)
            {
                var guestUser = db.Users.AsNoTracking().FirstOrDefault(u => u.Id == guestId);
                if (guestUser?.Source == WebAuthConstants.GuestSource)
                {
                    await messages.TransferWebConversationsAsync(guestId, user.Id);
                }
            }

            await webUsers.RecordLoginAsync(user.Id, AuthProvider.Email);
            await webUsers.SignInAsync(http, user.Id, isGuest: false);
            return Results.Ok(await BuildMeAsync(user.Id, webUsers, http));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static async Task<IResult> LogoutAsync(HttpContext http, WebUserService webUsers)
    {
        await webUsers.SignOutAsync(http);
        return Results.Ok(new { ok = true });
    }

    private static async Task<IResult> GetMeAsync(HttpContext http, WebUserService webUsers, UserService users)
    {
        var userId = await RequireUserAsync(http, webUsers);
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        return Results.Ok(await BuildMeAsync(userId.Value, webUsers, http));
    }

    private static IResult GetModelsAsync(UserService users, HttpContext http, WebUserService webUsers)
    {
        var userId = WebUserService.TryGetUserId(http);
        var preferred = userId is null
            ? GptModelCatalog.DefaultModelId
            : users.GetPreferredGptModel(userId.Value).Id;

        return Results.Ok(new
        {
            preferred,
            models = GptModelCatalog.All.Select(m => new
            {
                m.Id,
                m.DisplayName,
                m.StarsCost,
                m.AllowFreeQuota,
                m.Emoji,
            }),
        });
    }

    private static async Task<IResult> SetPreferredModelAsync(
        HttpContext http,
        SetModelRequest body,
        WebUserService webUsers,
        WebChatService chat,
        ApplicationContext db)
    {
        var userId = await RequireUserAsync(http, webUsers);
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        try
        {
            var language = db.BotSettings.FirstOrDefault(s => s.Id == userId)?.Language ?? "en";
            await chat.SetModelAsync(userId.Value, body.ModelId, language);
            await db.SaveChangesAsync();
            return Results.Ok(new { preferred = body.ModelId });
        }
        catch (ArgumentOutOfRangeException)
        {
            return Results.BadRequest(new { message = "Unknown model" });
        }
    }

    private static async Task<IResult> ListConversationsAsync(
        HttpContext http,
        WebUserService webUsers,
        MessageRepository messages)
    {
        var userId = await RequireUserAsync(http, webUsers);
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        var list = messages.ListConversations(userId.Value, userId.Value);
        return Results.Ok(list.Select(c => new
        {
            id = c.ContextId,
            title = c.Title,
            updatedAt = c.UpdatedAt,
            pinned = c.IsPinned,
        }));
    }

    private static async Task<IResult> GetConversationAsync(
        long contextId,
        HttpContext http,
        WebUserService webUsers,
        MessageRepository messages)
    {
        var userId = await RequireUserAsync(http, webUsers);
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        var items = messages.GetConversationMessages(userId.Value, userId.Value, contextId);
        if (items.Count == 0)
        {
            return Results.NotFound(new { message = "Conversation not found" });
        }

        return Results.Ok(new
        {
            id = contextId,
            messages = items.Select(m => new
            {
                id = m.Id,
                role = m.Role.ToString().ToLowerInvariant(),
                text = m.Text,
                createdAt = m.CreatedAt,
                type = m.Type?.ToString(),
            }),
        });
    }

    private static async Task<IResult> RenameConversationAsync(
        long contextId,
        HttpContext http,
        RenameConversationRequest body,
        WebUserService webUsers,
        MessageRepository messages)
    {
        var userId = await RequireUserAsync(http, webUsers);
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(body.Title))
        {
            return Results.BadRequest(new { message = "Title is required" });
        }

        var updated = await messages.RenameConversationAsync(userId.Value, contextId, body.Title);
        if (updated is null)
        {
            return Results.NotFound(new { message = "Conversation not found" });
        }

        return Results.Ok(new
        {
            id = updated.ContextId,
            title = updated.Title,
            updatedAt = updated.UpdatedAt,
            pinned = updated.IsPinned,
        });
    }

    private static async Task<IResult> PinConversationAsync(
        long contextId,
        HttpContext http,
        PinConversationRequest body,
        WebUserService webUsers,
        MessageRepository messages)
    {
        var userId = await RequireUserAsync(http, webUsers);
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        var updated = await messages.SetPinnedAsync(userId.Value, contextId, body.Pinned);
        if (updated is null)
        {
            return Results.NotFound(new { message = "Conversation not found" });
        }

        return Results.Ok(new
        {
            id = updated.ContextId,
            title = updated.Title,
            updatedAt = updated.UpdatedAt,
            pinned = updated.IsPinned,
        });
    }

    private static async Task<IResult> DeleteConversationAsync(
        long contextId,
        HttpContext http,
        WebUserService webUsers,
        MessageRepository messages)
    {
        var userId = await RequireUserAsync(http, webUsers);
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        var deleted = await messages.DeleteConversationAsync(userId.Value, contextId);
        if (!deleted)
        {
            return Results.NotFound(new { message = "Conversation not found" });
        }

        return Results.Ok(new { ok = true });
    }

    private static async Task ChatAsync(
        HttpContext http,
        ChatRequest body,
        WebUserService webUsers,
        WebChatService chat,
        CancellationToken cancellationToken)
    {
        var userId = await RequireUserAsync(http, webUsers);
        if (userId is null)
        {
            http.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        http.Response.Headers.ContentType = "text/event-stream";
        http.Response.Headers.CacheControl = "no-cache";
        http.Response.Headers.Connection = "keep-alive";

        await foreach (var chunk in chat.StreamChatAsync(
                           userId.Value,
                           body.Text ?? "",
                           body.NewConversation,
                           body.ContextId,
                           cancellationToken))
        {
            await http.Response.WriteAsync(chunk, cancellationToken);
            await http.Response.Body.FlushAsync(cancellationToken);
        }
    }

    private static async Task<IResult> GenerateImageAsync(
        HttpContext http,
        GenerateImageRequest body,
        WebUserService webUsers,
        WebMediaService media)
    {
        var userId = await RequireUserAsync(http, webUsers);
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        try
        {
            var result = await media.GenerateImageAsync(
                userId.Value,
                body.Prompt ?? "",
                body.Size,
                body.Quality,
                http.RequestAborted);
            return Results.Ok(new
            {
                mimeType = result.MimeType,
                base64 = result.Base64,
                starsCharged = result.StarsCharged,
            });
        }
        catch (InsufficientQuotaException ex)
        {
            return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status402PaymentRequired);
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static async Task<IResult> OcrAsync(
        HttpContext http,
        WebUserService webUsers,
        WebMediaService media)
    {
        var userId = await RequireUserAsync(http, webUsers);
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        if (!http.Request.HasFormContentType)
        {
            return Results.BadRequest(new { message = "multipart/form-data expected" });
        }

        var form = await http.Request.ReadFormAsync();
        var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
        if (file is null || file.Length == 0)
        {
            return Results.BadRequest(new { message = "file is required" });
        }

        await using var stream = file.OpenReadStream();
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);

        try
        {
            var text = await media.RecognizeTextAsync(userId.Value, ms.ToArray(), http.RequestAborted);
            return Results.Ok(new { text });
        }
        catch (InsufficientQuotaException ex)
        {
            return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status402PaymentRequired);
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static async Task<IResult> SttAsync(
        HttpContext http,
        WebUserService webUsers,
        WebMediaService media)
    {
        var userId = await RequireUserAsync(http, webUsers);
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        if (!http.Request.HasFormContentType)
        {
            return Results.BadRequest(new { message = "multipart/form-data expected" });
        }

        var form = await http.Request.ReadFormAsync();
        var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
        if (file is null || file.Length == 0)
        {
            return Results.BadRequest(new { message = "file is required" });
        }

        await using var stream = file.OpenReadStream();
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);

        try
        {
            var text = await media.TranscribeAsync(ms.ToArray(), http.RequestAborted);
            return Results.Ok(new { text });
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static IResult GetPublicConfigAsync()
    {
        var botUsername = Environment.GetEnvironmentVariable("TELEGRAM_BOT_USERNAME")
                          ?? AppConfig.BotName?.TrimStart('@')
                          ?? "GPTipsBot";
        return Results.Ok(new
        {
            botUsername,
            telegramLoginEnabled = !string.IsNullOrWhiteSpace(AppConfig.TelegramToken),
            yookassaEnabled = YooKassaConfig.IsEnabled,
        });
    }

    private static IResult GetPaymentPackagesAsync()
    {
        var packages = PaymentConfig.DepositStarPackages
            .Where(stars =>
                stars >= PaymentConfig.MinRechargeStars &&
                MoneyService.ToKopecks(stars) >= PaymentConfig.MinRechargeRub * 100L)
            .Select(stars => new
            {
                stars,
                rub = MoneyService.FormatRubAmount(stars),
                rubPerStar = YooKassaConfig.RubPerStar,
            })
            .ToList();

        return Results.Ok(new
        {
            enabled = YooKassaConfig.IsEnabled,
            rubPerStar = YooKassaConfig.RubPerStar,
            minRub = PaymentConfig.MinRechargeRub,
            minStars = PaymentConfig.MinRechargeStars,
            packages,
        });
    }

    private static async Task<IResult> CreateYooKassaPaymentAsync(
        HttpContext http,
        CreateYooKassaWebRequest body,
        MoneyService money)
    {
        var userId = RequireTelegramUser(http);
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        if (!YooKassaConfig.IsEnabled)
        {
            return Results.BadRequest(new { message = "YooKassa is not configured" });
        }

        if (body.Stars < PaymentConfig.MinRechargeStars)
        {
            return Results.BadRequest(new
            {
                message = $"Minimum top-up is {PaymentConfig.MinRechargeStars} Stars ({PaymentConfig.MinRechargeRub} RUB)",
            });
        }

        try
        {
            var returnUrl = BuildCabinetReturnUrl(http);
            var checkout = await money.CreateYooKassaCheckoutAsync(
                userId.Value,
                body.Stars,
                returnUrl,
                http.RequestAborted);

            return Results.Ok(new
            {
                invoiceId = checkout.InvoiceId,
                confirmationUrl = checkout.ConfirmationUrl,
                stars = checkout.Stars,
                rub = checkout.RubAmount,
            });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static async Task<IResult> SyncPaymentAsync(
        long invoiceId,
        HttpContext http,
        WebUserService webUsers,
        MoneyService money)
    {
        var userId = RequireTelegramUser(http);
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        var result = await money.SyncYooKassaInvoiceAsync(invoiceId, userId.Value, http.RequestAborted);
        var profile = await BuildMeAsync(userId.Value, webUsers, http);
        return Results.Ok(new
        {
            status = result.ToString(),
            credited = result == PaymentConfirmResult.DepositCredited,
            me = profile,
        });
    }

    private static string BuildCabinetReturnUrl(HttpContext http)
    {
        var configured = YooKassaConfig.ReturnUrl;
        // Prefer configured site URL; append cabinet marker for frontend polling.
        if (configured.Contains("t.me/", StringComparison.OrdinalIgnoreCase))
        {
            var request = http.Request;
            var origin = $"{request.Scheme}://{request.Host}";
            return $"{origin}/?cabinet=1";
        }

        var separator = configured.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        if (configured.Contains("cabinet=", StringComparison.OrdinalIgnoreCase))
        {
            return configured;
        }

        return $"{configured.TrimEnd('/')}{separator}cabinet=1";
    }

    private static long? RequireTelegramUser(HttpContext http)
    {
        var userId = WebUserService.TryGetUserId(http);
        if (userId is null || userId < 0 || WebUserService.IsGuest(http))
        {
            return null;
        }

        return userId;
    }

    private static async Task<long?> RequireUserAsync(HttpContext http, WebUserService webUsers)
    {
        var userId = WebUserService.TryGetUserId(http);
        if (userId is not null)
        {
            return userId;
        }

        // Auto-provision guest so chat works without an explicit login step (bota.chat style).
        var lang = http.Request.Headers.AcceptLanguage.ToString();
        var language = lang.StartsWith("ru", StringComparison.OrdinalIgnoreCase) ? "ru" : "en";
        var (user, isGuest) = await webUsers.EnsureGuestAsync(language);
        await webUsers.SignInAsync(http, user.Id, isGuest);
        return user.Id;
    }

    private static async Task<object> BuildMeAsync(long userId, WebUserService webUsers, HttpContext http)
    {
        var users = http.RequestServices.GetRequiredService<UserService>();
        var db = http.RequestServices.GetRequiredService<ApplicationContext>();
        var profile = await users.GetUserProfile(userId);
        var dbUser = db.Users.AsNoTracking().FirstOrDefault(u => u.Id == userId);
        return new
        {
            id = userId,
            isGuest = WebUserService.IsGuest(http) || userId < 0,
            firstName = profile.FirstName,
            lastName = profile.LastName,
            email = dbUser?.Email,
            emailConfirmed = dbUser?.EmailConfirmed == true,
            stars = profile.Stars,
            free = new
            {
                gpt = profile.GptRequests,
                images = profile.Images,
                ocr = profile.ImageTexts,
                animations = profile.PhotoAnimations,
                summaries = profile.Summaries,
            },
            model = new
            {
                id = profile.GptModelId,
                name = profile.GptModelDisplayName,
            },
        };
    }

    public sealed record TelegramLoginRequest(
        long Id,
        string? FirstName,
        string? LastName,
        string? Username,
        string? PhotoUrl,
        long AuthDate,
        string? Hash,
        long? PreviousGuestId = null);

    public sealed record EmailRegisterRequest(string? Email, string? Password, string? FirstName);
    public sealed record EmailConfirmRequest(string? Email, string? Code, long? PreviousGuestId = null);
    public sealed record EmailResendRequest(string? Email);
    public sealed record EmailLoginRequest(string? Email, string? Password, long? PreviousGuestId = null);

    public sealed record CreateYooKassaWebRequest(int Stars);

    public sealed record SetModelRequest(string ModelId);

    public sealed record ChatRequest(string? Text, bool NewConversation = false, long? ContextId = null);

    public sealed record RenameConversationRequest(string? Title);

    public sealed record PinConversationRequest(bool Pinned);

    public sealed record GenerateImageRequest(string? Prompt, string? Size, string? Quality);
}

public static class ImagePresets
{
    public static readonly object[] All =
    [
        new { id = "photo-restoration", title = "Photo restoration", prompt = "Restore this old photo: improve clarity, fix scratches, natural colors" },
        new { id = "anime-portrait", title = "Anime portrait", prompt = "Create an anime-style portrait, clean lineart, soft lighting" },
        new { id = "youtube-thumbnail", title = "YouTube thumbnail", prompt = "Bold YouTube thumbnail, high contrast, expressive face, readable title space" },
        new { id = "infographic", title = "Infographic", prompt = "Clean modern infographic illustration, labeled parts, white background" },
        new { id = "pixel-art", title = "8-bit game", prompt = "8-bit pixel art game scene, vibrant colors, retro vibe" },
        new { id = "underwater", title = "Underwater portrait", prompt = "Cinematic underwater portrait with caustic light patterns" },
        new { id = "hairstyle", title = "Hairstyle options", prompt = "Fashion hairstyle lookbook collage, studio lighting" },
        new { id = "palm-reading", title = "Palm reading", prompt = "Mystical palmistry illustration highlighting hand lines" },
    ];
}
