using GPTipsBot.Config;
using GPTipsBot.Services;
using GPTipsBot.Services.LavaTop;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace GPTipsBot.Webhooks;

public static class LavaTopWebhookEndpoint
{
    public static void MapLavaTopWebhook(this WebApplication app)
    {
        app.MapMethods("/webhooks/lavatop", ["GET", "HEAD"], () => Results.Ok("lava.top webhook ready"));
        app.MapPost("/webhooks/lavatop", HandleAsync);
    }

    private static async Task<IResult> HandleAsync(
        HttpRequest request,
        MoneyService moneyService,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("LavaTopWebhook");

        if (!LavaTopConfig.IsEnabled || string.IsNullOrWhiteSpace(LavaTopConfig.WebhookApiKey))
        {
            logger.LogWarning("Webhook rejected: lava.top is not configured (missing api key/offer/webhook key)");
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        // Unlike YooKassa's advisory IP allowlist, this is a real shared secret we chose ourselves when
        // adding the webhook in the lava.top dashboard ("API key" auth) — a mismatch is hard-rejected.
        var providedKey = request.Headers["X-Api-Key"].ToString();
        if (!string.Equals(providedKey, LavaTopConfig.WebhookApiKey, StringComparison.Ordinal))
        {
            logger.LogWarning("Webhook rejected: X-Api-Key mismatch");
            return Results.Unauthorized();
        }

        using var reader = new StreamReader(request.Body);
        var body = await reader.ReadToEndAsync(cancellationToken);
        logger.LogInformation("Webhook body length={Length} preview={Preview}",
            body.Length,
            body.Length <= 500 ? body : body[..500] + "...");

        LavaTopWebhookPayload? payload;
        try
        {
            payload = JsonConvert.DeserializeObject<LavaTopWebhookPayload>(body);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Webhook body JSON parse failed");
            return Results.BadRequest();
        }

        if (payload == null || string.IsNullOrWhiteSpace(payload.ContractId))
        {
            logger.LogWarning("Webhook ignored: no contractId in payload");
            return Results.Ok();
        }

        logger.LogInformation(
            "Webhook parsed: eventType={EventType} contractId={ContractId} status={Status} amount={Amount} {Currency}",
            payload.EventType,
            payload.ContractId,
            payload.Status,
            payload.Amount,
            payload.Currency);

        if (!string.Equals(payload.EventType, "payment.success", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInformation("Webhook ignored: eventType={EventType} (only payment.success is processed)",
                payload.EventType);
            return Results.Ok();
        }

        try
        {
            var result = await moneyService.ConfirmLavaTopPaymentAsync(payload.ContractId, cancellationToken);
            logger.LogInformation(
                "Webhook confirm finished: contractId={ContractId} result={Result}",
                payload.ContractId,
                result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Webhook confirm failed for contract {ContractId}", payload.ContractId);
            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }

        return Results.Ok();
    }
}
