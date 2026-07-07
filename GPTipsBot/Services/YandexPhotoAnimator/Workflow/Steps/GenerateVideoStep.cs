using Microsoft.Extensions.Logging;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace GPTipsBot.Services.YandexPhotoAnimator.Workflow.Steps;

public class GenerateVideoStep(
    YaPhotoAnimatorService animator,
    PhotoAnimationProgressNotifier progressNotifier,
    ILogger<GenerateVideoStep> logger) : StepBodyAsync
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
                logger.LogWarning("Photo animation generation was not started for chat {ChatId}", data.ChatId);
                data.ErrorMessage = PhotoAnimationWorkflowErrors.Failed;
                return ExecutionResult.Next();
            }

            data.GenerationId = response.VideoGeneration.Id;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start photo animation for chat {ChatId}", data.ChatId);
            data.ErrorMessage = PhotoAnimationWorkflowErrors.Failed;
        }

        return ExecutionResult.Next();
    }
}
