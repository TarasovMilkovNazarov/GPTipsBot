using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace GPTipsBot.Services.YandexPhotoAnimator.Workflow.Steps;

public class GenerateVideoStep(
    YaPhotoAnimatorService animator,
    PhotoAnimationProgressNotifier progressNotifier) : StepBodyAsync
{
    public override async Task<ExecutionResult> RunAsync(IStepExecutionContext context)
    {
        var data = (PhotoAnimationWorkflowData)context.Workflow.Data;
        if (!string.IsNullOrEmpty(data.ErrorMessage))
        {
            return ExecutionResult.Next();
        }

        try
        {
            if (data.ProgressMessageId.HasValue)
            {
                await progressNotifier.ReportStartingGenerationAsync(data.ChatId, data.ProgressMessageId.Value);
            }

            var response = await animator.GenerateVideo(data.ImageUrl!, data.Prompt);
            if (response?.VideoGeneration?.Id == null)
            {
                data.ErrorMessage = "Не удалось запустить генерацию видео";
                return ExecutionResult.Next();
            }

            data.GenerationId = response.VideoGeneration.Id;
        }
        catch (Exception ex)
        {
            data.ErrorMessage = ex.Message;
        }

        return ExecutionResult.Next();
    }
}
