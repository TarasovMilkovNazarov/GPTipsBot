using GPTipsBot.Localization;
using Microsoft.Extensions.Logging;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace GPTipsBot.Services.YandexPhotoAnimator.Workflow.Steps;

public class WaitForAliceImageStep(
    YaPhotoAnimatorService animator,
    PhotoAnimationProgressNotifier progressNotifier,
    ILogger<WaitForAliceImageStep> logger) : StepBodyAsync
{
    public string GenerationId { get; set; } = string.Empty;
    public AliceImageKind Kind { get; set; }
    public long ChatId { get; set; }
    public int? ProgressMessageId { get; set; }
    public int InitialRemainingTimeSec { get; set; }
    public int LastReportedRemainingSec { get; set; }
    public DateTime? DeadlineUtc { get; set; }
    public string? ResultImageUrl { get; set; }
    public string? ErrorMessage { get; set; }

    public override async Task<ExecutionResult> RunAsync(IStepExecutionContext context)
    {
        var data = (AliceImageWorkflowData)context.Workflow.Data;
        using var _ = UiCultureScope.ForLanguage(data.UiLanguage);

        if (!string.IsNullOrEmpty(data.ErrorMessage) || !string.IsNullOrEmpty(ErrorMessage))
        {
            ErrorMessage ??= data.ErrorMessage;
            ResultImageUrl ??= data.ResultImageUrl;
            return ExecutionResult.Next();
        }

        if (string.IsNullOrEmpty(GenerationId))
        {
            ErrorMessage = PhotoAnimationWorkflowErrors.Failed;
            return ExecutionResult.Next();
        }

        if (!string.IsNullOrEmpty(data.ResultImageUrl))
        {
            ResultImageUrl = data.ResultImageUrl;
            return ExecutionResult.Next();
        }

        try
        {
            var result = Kind == AliceImageKind.Combining
                ? await animator.GetCombiningStatus(GenerationId)
                : await animator.GetEditingStatus(GenerationId);

            var remainingSeconds = ResolveRemainingSeconds(result);
            if (!string.IsNullOrEmpty(result.ImageUrl))
            {
                logger.LogInformation(
                    "Alice {Kind} ready for chat {ChatId}. Status={Status}, ImageUrl set",
                    Kind,
                    ChatId,
                    result.Status);
                ResultImageUrl = result.ImageUrl;
                return ExecutionResult.Next();
            }

            if (InitialRemainingTimeSec == 0)
            {
                InitialRemainingTimeSec = Math.Max(remainingSeconds, PhotoAnimationWaitPolicy.MinEstimatedSeconds);
                var timeoutSeconds = PhotoAnimationWaitPolicy.CalculateTimeoutSeconds(InitialRemainingTimeSec);
                DeadlineUtc = DateTime.UtcNow.AddSeconds(timeoutSeconds);
            }

            if (DeadlineUtc.HasValue && DateTime.UtcNow >= DeadlineUtc.Value)
            {
                logger.LogWarning("Alice {Kind} timed out for chat {ChatId}", Kind, ChatId);
                ErrorMessage = PhotoAnimationWorkflowErrors.Failed;
                return ExecutionResult.Next();
            }

            if (ProgressMessageId.HasValue
                && PhotoAnimationWaitPolicy.ShouldUpdateProgress(LastReportedRemainingSec, remainingSeconds))
            {
                await progressNotifier.ReportImageWaitingAsync(
                    ChatId,
                    ProgressMessageId.Value,
                    remainingSeconds);
                LastReportedRemainingSec = remainingSeconds;
            }

            var pollIntervalSec = PhotoAnimationWaitPolicy.GetPollIntervalSeconds(remainingSeconds);
            return ExecutionResult.Sleep(TimeSpan.FromSeconds(pollIntervalSec), remainingSeconds);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed while waiting for Alice {Kind} in chat {ChatId}", Kind, ChatId);
            ErrorMessage = PhotoAnimationWorkflowErrors.Failed;
            return ExecutionResult.Next();
        }
    }

    private static int ResolveRemainingSeconds(AliceImageGenerationResult result)
    {
        if (result.RemainingTimeSec > 0)
        {
            return result.RemainingTimeSec;
        }

        return result.EstimateTimeSec > 0 ? result.EstimateTimeSec : 0;
    }
}
