using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace GPTipsBot.Services.YandexPhotoAnimator.Workflow.Steps;

public class WaitForVideoStep(
    YaPhotoAnimatorService animator,
    PhotoAnimationProgressNotifier progressNotifier) : StepBodyAsync
{
    private const int MaxAttempts = 60;

    public override async Task<ExecutionResult> RunAsync(IStepExecutionContext context)
    {
        var data = (PhotoAnimationWorkflowData)context.Workflow.Data;
        if (!string.IsNullOrEmpty(data.ErrorMessage))
        {
            return ExecutionResult.Next();
        }

        try
        {
            var result = await animator.GetGenerationStatus(data.GenerationId!);

            if (!string.IsNullOrEmpty(result.VideoGeneration.VideoURL))
            {
                data.VideoUrl = result.VideoGeneration.VideoURL;
                return ExecutionResult.Next();
            }

            data.PollAttempt++;

            if (data.PollAttempt > MaxAttempts)
            {
                data.ErrorMessage = "Превышено время ожидания генерации видео";
                return ExecutionResult.Next();
            }

            if (data.ProgressMessageId.HasValue)
            {
                await progressNotifier.ReportWaitingAsync(
                    data.ChatId,
                    data.ProgressMessageId.Value,
                    result.VideoGeneration.RemainingTimeSec,
                    data.PollAttempt,
                    MaxAttempts);
            }

            var delaySeconds = Math.Max(result.VideoGeneration.RemainingTimeSec, 5);
            return ExecutionResult.Sleep(TimeSpan.FromSeconds(delaySeconds), null);
        }
        catch (Exception ex)
        {
            data.ErrorMessage = ex.Message;
            return ExecutionResult.Next();
        }
    }
}
