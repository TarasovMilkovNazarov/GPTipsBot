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
        app.MapGet("/", () => Results.Ok("GPTipsBot"));
        app.MapGet("/health", () => Results.Ok("ok"));
        app.MapMethods("/webhooks/yookassa", ["GET", "HEAD"], () => Results.Ok("yookassa webhook ready"));
        app.MapPost("/webhooks/yookassa", HandleAsync);
        app.MapFallback(() => Results.NotFound("not found"));
    }

    private static async Task<IResult> HandleAsync(
        HttpRequest request,
        MoneyService moneyService,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("YooKassaWebhook");
        var remoteIp = request.HttpContext.Connection.RemoteIpAddress;
        var forwardedFor = request.Headers["X-Forwarded-For"].ToString();
        var realIp = request.Headers["X-Real-IP"].ToString();

        logger.LogInformation(
            "Webhook received: method={Method} path={Path} remoteIp={RemoteIp} xff={XForwardedFor} xRealIp={XRealIp} contentLength={ContentLength} yooEnabled={YooEnabled} secretFormatOk={SecretOk}",
            request.Method,
            request.Path.Value,
            remoteIp,
            forwardedFor,
            realIp,
            request.ContentLength,
            YooKassaConfig.IsEnabled,
            YooKassaConfig.HasValidSecretKeyFormat);

        if (!YooKassaConfig.IsEnabled)
        {
            logger.LogWarning("Webhook rejected: YooKassa is not configured (missing shop id/secret)");
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        // IP allowlist is advisory: real auth is GetPaymentAsync with shop secret.
        // Behind Caddy, RemoteIp may be the proxy if ForwardedHeaders misconfigured;
        // hard 401 then drops all YooKassa notifications.
        var ipAllowed = !AppConfig.IsProduction ||
                        YooKassaIpAllowList.IsAllowed(remoteIp) ||
                        YooKassaIpAllowList.IsAllowedFromForwarded(forwardedFor);

        logger.LogInformation(
            "Webhook IP check: isProduction={IsProduction} remoteIp={RemoteIp} xff={XForwardedFor} allowed={Allowed}",
            AppConfig.IsProduction,
            remoteIp,
            forwardedFor,
            ipAllowed);

        if (!ipAllowed)
        {
            logger.LogWarning(
                "Webhook IP not in YooKassa allowlist (continuing; payment will be verified via API): remoteIp={RemoteIp} xff={XForwardedFor}",
                remoteIp,
                forwardedFor);
        }

        using var reader = new StreamReader(request.Body);
        var body = await reader.ReadToEndAsync(cancellationToken);
        logger.LogInformation("Webhook body length={Length} preview={Preview}",
            body.Length,
            body.Length <= 500 ? body : body[..500] + "...");

        YooKassaNotification? notification;
        try
        {
            notification = JsonConvert.DeserializeObject<YooKassaNotification>(body, JsonSettings);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Webhook body JSON parse failed");
            return Results.BadRequest();
        }

        if (notification == null)
        {
            logger.LogWarning("Webhook deserialized to null notification");
            return Results.Ok();
        }

        logger.LogInformation(
            "Webhook parsed: type={Type} event={Event} paymentId={PaymentId} status={Status} paid={Paid} amount={Amount} {Currency}",
            notification.Type,
            notification.Event,
            notification.Object?.Id,
            notification.Object?.Status,
            notification.Object?.Paid,
            notification.Object?.Amount?.Value,
            notification.Object?.Amount?.Currency);

        if (!string.Equals(notification.Event, "payment.succeeded", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInformation("Webhook ignored: event={Event} (only payment.succeeded is processed)",
                notification.Event);
            return Results.Ok();
        }

        if (string.IsNullOrWhiteSpace(notification.Object?.Id))
        {
            logger.LogWarning("Webhook ignored: payment.succeeded without payment id");
            return Results.Ok();
        }

        try
        {
            logger.LogInformation("Webhook confirming payment {PaymentId}", notification.Object.Id);
            var result = await moneyService.ConfirmYooKassaPaymentAsync(notification.Object.Id, cancellationToken);
            logger.LogInformation(
                "Webhook confirm finished: paymentId={PaymentId} result={Result}",
                notification.Object.Id,
                result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Webhook confirm failed for payment {PaymentId}", notification.Object.Id);
            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }

        return Results.Ok();
    }
}
