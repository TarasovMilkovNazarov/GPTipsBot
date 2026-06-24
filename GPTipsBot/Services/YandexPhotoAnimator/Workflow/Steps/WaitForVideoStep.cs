using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace GPTipsBot.Services.YandexPhotoAnimator.Workflow.Steps;

public class WaitForVideoStep(
    YaPhotoAnimatorService animator,
    PhotoAnimationProgressNotifier progressNotifier) : StepBodyAsync
{
    private const int MaxAttempts = 60;

    public string GenerationId { get; set; } = string.Empty;
    public long ChatId { get; set; }
    public int? ProgressMessageId { get; set; }
    public int PollAttempt { get; set; }
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

            if (!string.IsNullOrEmpty(result.VideoGeneration.VideoURL))
            {
                VideoUrl = result.VideoGeneration.VideoURL;
                return ExecutionResult.Next();
            }

            PollAttempt++;

            if (PollAttempt > MaxAttempts)
            {
                ErrorMessage = "Превышено время ожидания генерации видео";
                return ExecutionResult.Next();
            }

            if (ProgressMessageId.HasValue)
            {
                await progressNotifier.ReportWaitingAsync(
                    ChatId,
                    ProgressMessageId.Value,
                    result.VideoGeneration.RemainingTimeSec,
                    PollAttempt,
                    MaxAttempts);
            }

            var delaySeconds = Math.Max(result.VideoGeneration.RemainingTimeSec, 5);
            return ExecutionResult.Sleep(TimeSpan.FromSeconds(delaySeconds), PollAttempt);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            return ExecutionResult.Next();
        }
    }
}
