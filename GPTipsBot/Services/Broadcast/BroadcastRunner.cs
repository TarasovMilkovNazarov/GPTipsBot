using System.Threading.Channels;
using GPTipsBot.Config;
using GPTipsBot.Db;
using GPTipsBot.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace GPTipsBot.Services.Broadcast;

public sealed class BroadcastRunner(
    IServiceScopeFactory scopeFactory,
    ITelegramBotClient botClient,
    ILogger<BroadcastRunner> logger) : BackgroundService
{
    private readonly Channel<long> _queue = Channel.CreateUnbounded<long>();
    private readonly object _gate = new();
    private CancellationTokenSource? _runCts;
    private long? _runningCampaignId;

    public bool IsRunning
    {
        get
        {
            lock (_gate)
            {
                return _runningCampaignId != null;
            }
        }
    }

    public bool TryEnqueue(long campaignId)
    {
        lock (_gate)
        {
            if (_runningCampaignId != null)
            {
                return false;
            }

            _runningCampaignId = campaignId;
        }

        if (!_queue.Writer.TryWrite(campaignId))
        {
            lock (_gate)
            {
                if (_runningCampaignId == campaignId)
                {
                    _runningCampaignId = null;
                }
            }

            return false;
        }

        return true;
    }

    public void RequestStop()
    {
        lock (_gate)
        {
            _runCts?.Cancel();
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var campaignId in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            using var runCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            lock (_gate)
            {
                _runCts = runCts;
                _runningCampaignId = campaignId;
            }

            try
            {
                await RunCampaignAsync(campaignId, runCts.Token);
            }
            catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
            {
                await MarkStatusAsync(campaignId, BroadcastStatus.Cancelled, "Остановлено админом.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Broadcast {CampaignId} failed", campaignId);
                await MarkStatusAsync(campaignId, BroadcastStatus.Failed, Truncate(ex.Message, 500));
            }
            finally
            {
                lock (_gate)
                {
                    if (_runningCampaignId == campaignId)
                    {
                        _runningCampaignId = null;
                    }

                    _runCts = null;
                }
            }
        }
    }

    private async Task RunCampaignAsync(long campaignId, CancellationToken token)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var service = scope.ServiceProvider.GetRequiredService<BroadcastService>();

        var campaign = await db.BroadcastCampaigns.FirstOrDefaultAsync(c => c.Id == campaignId, token);
        if (campaign == null)
        {
            return;
        }

        var config = BroadcastService.Deserialize(campaign.ConfigJson);
        var error = BroadcastTextParser.Validate(config);
        if (error != null)
        {
            campaign.Status = BroadcastStatus.Failed;
            campaign.LastError = error;
            campaign.FinishedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(token);
            await NotifyAdminsAsync(campaign, token);
            return;
        }

        campaign.Status = BroadcastStatus.Running;
        campaign.StartedAt ??= DateTimeOffset.UtcNow;
        campaign.LastError = null;
        if (campaign.TotalTargeted == 0)
        {
            campaign.TotalTargeted = await service.CountAudienceAsync(config, token);
        }

        await db.SaveChangesAsync(token);
        await NotifyAdminsAsync(campaign, token);

        var delay = TimeSpan.FromMilliseconds(1000d / config.MessagesPerSecond);
        var lastProgressAt = campaign.SentCount + campaign.FailedCount + campaign.BlockedCount + campaign.SkippedCount;

        while (!token.IsCancellationRequested)
        {
            var batch = await service.TakeBatchAsync(config, campaign.LastUserId, config.BatchSize, token);
            if (batch.Count == 0)
            {
                campaign.Status = BroadcastStatus.Completed;
                campaign.FinishedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(token);
                await NotifyAdminsAsync(campaign, token);
                return;
            }

            var blockedIds = new List<long>();
            foreach (var recipient in batch)
            {
                token.ThrowIfCancellationRequested();
                var result = await SendOneAsync(config, recipient, token);
                campaign.LastUserId = recipient.UserId;
                switch (result)
                {
                    case SendResult.Sent:
                        campaign.SentCount++;
                        break;
                    case SendResult.Blocked:
                        campaign.BlockedCount++;
                        blockedIds.Add(recipient.UserId);
                        break;
                    case SendResult.Skipped:
                        campaign.SkippedCount++;
                        break;
                    default:
                        campaign.FailedCount++;
                        break;
                }

                var processed = campaign.SentCount + campaign.FailedCount + campaign.BlockedCount +
                                campaign.SkippedCount;
                if (processed - lastProgressAt >= config.ProgressEvery)
                {
                    lastProgressAt = processed;
                    await db.SaveChangesAsync(token);
                    await NotifyAdminsAsync(campaign, token);
                }

                await Task.Delay(delay, token);
            }

            if (blockedIds.Count > 0)
            {
                await service.MarkBlockedAsync(blockedIds, token);
            }

            await db.SaveChangesAsync(token);
        }

        token.ThrowIfCancellationRequested();
    }

    private async Task<SendResult> SendOneAsync(
        BroadcastCampaignConfig config,
        BroadcastRecipient recipient,
        CancellationToken token)
    {
        var text = BroadcastTextParser.ResolveText(config, recipient.Language);
        if (string.IsNullOrWhiteSpace(text) || recipient.TelegramId == 0)
        {
            return SendResult.Skipped;
        }

        ReplyMarkup? markup = null;
        if (config.Buttons is { Count: > 0 })
        {
            markup = new InlineKeyboardMarkup(
                config.Buttons
                    .Where(b => !string.IsNullOrWhiteSpace(b.Text) && !string.IsNullOrWhiteSpace(b.Url))
                    .Select(b => InlineKeyboardButton.WithUrl(b.Text, b.Url))
                    .ToArray());
        }

        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                await SendBroadcastMessageAsync(config, recipient.TelegramId, text, markup, token);
                return SendResult.Sent;
            }
            catch (ApiRequestException ex) when (ex.ErrorCode == 429)
            {
                var wait = Math.Max(ex.Parameters?.RetryAfter ?? 3, 1);
                logger.LogWarning("Broadcast flood wait {Seconds}s for {TelegramId}", wait, recipient.TelegramId);
                await Task.Delay(TimeSpan.FromSeconds(wait + 1), token);
            }
            catch (ApiRequestException ex) when (IsBlocked(ex))
            {
                return SendResult.Blocked;
            }
            catch (ApiRequestException ex) when (ex.Message.Contains("can't parse entities", StringComparison.OrdinalIgnoreCase)
                                                 && config.ToTelegramParseMode() != null)
            {
                logger.LogWarning(ex, "Broadcast parse failed for {TelegramId}, retrying as plain", recipient.TelegramId);
                try
                {
                    var plain = ClonePlain(config);
                    await SendBroadcastMessageAsync(plain, recipient.TelegramId, text, markup, token);
                    return SendResult.Sent;
                }
                catch (Exception retryEx)
                {
                    logger.LogError(retryEx, "Broadcast plain retry failed for {TelegramId}", recipient.TelegramId);
                    return SendResult.Failed;
                }
            }
            catch (ApiRequestException ex)
            {
                logger.LogError(ex, "Broadcast send failed for {TelegramId}", recipient.TelegramId);
                return SendResult.Failed;
            }
        }

        return SendResult.Failed;
    }

    private Task SendBroadcastMessageAsync(
        BroadcastCampaignConfig config,
        long telegramId,
        string text,
        ReplyMarkup? markup,
        CancellationToken token)
    {
        var parseMode = config.ToTelegramParseMode();
        var preview = config.DisableWebPagePreview
            ? new Telegram.Bot.Types.LinkPreviewOptions { IsDisabled = true }
            : null;
        if (parseMode is { } mode)
        {
            return botClient.SendMessage(
                telegramId,
                text,
                parseMode: mode,
                linkPreviewOptions: preview,
                disableNotification: config.DisableNotification,
                replyMarkup: markup,
                cancellationToken: token);
        }

        return botClient.SendMessage(
            telegramId,
            text,
            linkPreviewOptions: preview,
            disableNotification: config.DisableNotification,
            replyMarkup: markup,
            cancellationToken: token);
    }

    private static BroadcastCampaignConfig ClonePlain(BroadcastCampaignConfig config)
    {
        var clone = BroadcastService.Deserialize(BroadcastService.Serialize(config));
        clone.ParseModeName = "plain";
        return clone;
    }

    private static bool IsBlocked(ApiRequestException ex)
    {
        if (ex.ErrorCode == 403)
        {
            return true;
        }

        if (ex.ErrorCode != 400)
        {
            return false;
        }

        var message = ex.Message ?? "";
        return message.Contains("bot was blocked", StringComparison.OrdinalIgnoreCase)
               || message.Contains("chat not found", StringComparison.OrdinalIgnoreCase)
               || message.Contains("user is deactivated", StringComparison.OrdinalIgnoreCase)
               || message.Contains("PEER_ID_INVALID", StringComparison.OrdinalIgnoreCase);
    }

    private async Task MarkStatusAsync(long campaignId, BroadcastStatus status, string? error)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
            var campaign = await db.BroadcastCampaigns.FirstOrDefaultAsync(c => c.Id == campaignId);
            if (campaign == null)
            {
                return;
            }

            campaign.Status = status;
            campaign.FinishedAt = DateTimeOffset.UtcNow;
            campaign.LastError = error;
            await db.SaveChangesAsync();
            await NotifyAdminsAsync(campaign, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to persist broadcast {CampaignId} status {Status}", campaignId, status);
        }
    }

    private async Task NotifyAdminsAsync(BroadcastCampaign campaign, CancellationToken token)
    {
        var text = BroadcastService.FormatProgress(campaign);
        foreach (var adminId in AppConfig.AdminIds)
        {
            try
            {
                await botClient.SendMessage(adminId, text, cancellationToken: token);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not notify admin {AdminId} about broadcast {CampaignId}", adminId, campaign.Id);
            }
        }
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];

    private enum SendResult
    {
        Sent,
        Blocked,
        Failed,
        Skipped,
    }
}
