using GPTipsBot.Config;
using GPTipsBot.Services;
using GPTipsBot.Services.YooKassa;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace GPTipsBot.Webhooks;

public static class YooKassaWebhookEndpoint
{
    private static readonly JsonSerializerSettings JsonSettings = new()
    {
        ContractResolver = new DefaultContractResolver
        {
            NamingStrategy = new SnakeCaseNamingStrategy()
        }
    };

    public static void MapYooKassaWebhook(this WebApplication app)
    {
        app.MapPost("/webhooks/yookassa", HandleAsync);
        app.MapGet("/health", () => Results.Ok("ok"));
    }

    private static async Task<IResult> HandleAsync(
        HttpRequest request,
        MoneyService moneyService,
        ILogger<MoneyService> logger,
        CancellationToken cancellationToken)
    {
        if (!YooKassaConfig.IsEnabled)
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        var remoteIp = request.HttpContext.Connection.RemoteIpAddress;
        if (AppConfig.IsProduction && !YooKassaIpAllowList.IsAllowed(remoteIp))
        {
            logger.LogWarning("Rejected YooKassa webhook from {RemoteIp}", remoteIp);
            return Results.Unauthorized();
        }

        using var reader = new StreamReader(request.Body);
        var body = await reader.ReadToEndAsync(cancellationToken);
        YooKassaNotification? notification;
        try
        {
            notification = JsonConvert.DeserializeObject<YooKassaNotification>(body, JsonSettings);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Invalid YooKassa webhook body");
            return Results.BadRequest();
        }

        if (notification == null ||
            !string.Equals(notification.Event, "payment.succeeded", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(notification.Object?.Id))
        {
            return Results.Ok();
        }

        try
        {
            await moneyService.ConfirmYooKassaPaymentAsync(notification.Object.Id, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process YooKassa payment {PaymentId}", notification.Object.Id);
            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }

        return Results.Ok();
    }
}
