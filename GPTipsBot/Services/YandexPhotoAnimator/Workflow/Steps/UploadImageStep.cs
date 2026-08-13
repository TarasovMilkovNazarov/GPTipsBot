using GPTipsBot.Localization;
using Microsoft.Extensions.Logging;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace GPTipsBot.Services.YandexPhotoAnimator.Workflow.Steps;

public class UploadImageStep(
    YaPhotoAnimatorService animator,
    PhotoAnimationProgressNotifier progressNotifier,
    ILogger<UploadImageStep> logger) : StepBodyAsync
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
                await progressNotifier.ReportUploadingImageAsync(data.ChatId, data.ProgressMessageId.Value);
            }

            data.ImageUrl = await animator.UploadImageFromBase64(data.Base64Image!);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to upload image for animation in chat {ChatId}", data.ChatId);
            data.ErrorMessage = PhotoAnimationWorkflowErrors.Failed;
        }

        return ExecutionResult.Next();
    }
}
