using Microsoft.Extensions.Logging;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace GPTipsBot.Services.YandexPhotoAnimator.Workflow.Steps;

public class WaitForVideoStep(
    YaPhotoAnimatorService animator,
    PhotoAnimationProgressNotifier progressNotifier,
    ILogger<WaitForVideoStep> logger) : StepBodyAsync
{
    public string GenerationId { get; set; } = string.Empty;
    public long ChatId { get; set; }
    public int? ProgressMessageId { get; set; }
    public int InitialRemainingTimeSec { get; set; }
    public int LastReportedRemainingSec { get; set; }
    public DateTime? DeadlineUtc { get; set; }
    public string? VideoUrl { get; set; }
    public string? ErrorMessage { get; set; }

    public override async Task<ExecutionResult> RunAsync(IStepExecutionContext context)
    {
        if (!string.IsNullOrEmpty(ErrorMessage))
        {
            return ExecutionResult.Next();
        }

        try
        {
            var result = await animator.GetGenerationStatus(GenerationId);
            var remainingSeconds = Math.Max(result.VideoGeneration.RemainingTimeSec, 0);

            if (!string.IsNullOrEmpty(result.VideoGeneration.VideoURL))
            {
                VideoUrl = result.VideoGeneration.VideoURL;
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
                logger.LogWarning("Photo animation timed out for chat {ChatId}", ChatId);
                ErrorMessage = PhotoAnimationWorkflowErrors.Failed;
                return ExecutionResult.Next();
            }

            if (ProgressMessageId.HasValue
                && PhotoAnimationWaitPolicy.ShouldUpdateProgress(LastReportedRemainingSec, remainingSeconds))
            {
                await progressNotifier.ReportWaitingAsync(
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
            logger.LogError(ex, "Failed while waiting for photo animation in chat {ChatId}", ChatId);
            ErrorMessage = PhotoAnimationWorkflowErrors.Failed;
            return ExecutionResult.Next();
        }
    }
}
