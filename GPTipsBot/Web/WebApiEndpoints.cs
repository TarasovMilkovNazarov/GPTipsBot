using System.Security.Claims;
using GPTipsBot.Config;
using GPTipsBot.Db;
using GPTipsBot.Repositories;
using GPTipsBot.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace GPTipsBot.Web;

public static class WebApiEndpoints
{
    public static void MapWebApi(this WebApplication app)
    {
        var api = app.MapGroup("/api");

        api.MapPost("/auth/guest", EnsureGuestAsync);
        api.MapPost("/auth/telegram", TelegramLoginAsync);
        api.MapPost("/auth/logout", LogoutAsync);
        api.MapGet("/me", GetMeAsync);
        api.MapGet("/models", GetModelsAsync);
        api.MapPut("/models/preferred", SetPreferredModelAsync);
        api.MapGet("/conversations", ListConversationsAsync);
        api.MapGet("/conversations/{contextId:long}", GetConversationAsync);
        api.MapPost("/chat", ChatAsync);
        api.MapPost("/images/generate", GenerateImageAsync);
        api.MapPost("/ocr", OcrAsync);
        api.MapPost("/stt", SttAsync);
        api.MapGet("/presets/images", () => Results.Ok(ImagePresets.All));
        api.MapGet("/config/public", GetPublicConfigAsync);
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
        await webUsers.SignInAsync(http, user.Id, isGuest);
        return Results.Ok(await BuildMeAsync(user.Id, webUsers, http));
    }

    private static async Task<IResult> TelegramLoginAsync(
        HttpContext http,
        TelegramLoginRequest body,
        WebUserService webUsers)
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

        var lang = http.Request.Headers.AcceptLanguage.ToString();
        var language = lang.StartsWith("ru", StringComparison.OrdinalIgnoreCase) ? "ru" : "en";
        var (user, isGuest) = await webUsers.EnsureTelegramUserAsync(payload, language);
        await webUsers.SignInAsync(http, user.Id, isGuest);
        return Results.Ok(await BuildMeAsync(user.Id, webUsers, http));
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
        });
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
        var profile = await users.GetUserProfile(userId);
        return new
        {
            id = userId,
            isGuest = WebUserService.IsGuest(http) || userId < 0,
            firstName = profile.FirstName,
            lastName = profile.LastName,
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
        string? Hash);

    public sealed record SetModelRequest(string ModelId);

    public sealed record ChatRequest(string? Text, bool NewConversation = false, long? ContextId = null);

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
