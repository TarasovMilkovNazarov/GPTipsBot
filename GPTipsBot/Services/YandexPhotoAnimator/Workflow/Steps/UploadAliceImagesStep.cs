using GPTipsBot.Localization;
using Microsoft.Extensions.Logging;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace GPTipsBot.Services.YandexPhotoAnimator.Workflow.Steps;

public class UploadAliceImagesStep(
    YaPhotoAnimatorService animator,
    PhotoAnimationProgressNotifier progressNotifier,
    ILogger<UploadAliceImagesStep> logger) : StepBodyAsync
{
    public override async Task<ExecutionResult> RunAsync(IStepExecutionContext context)
    {
        var data = (AliceImageWorkflowData)context.Workflow.Data;
        using var _ = UiCultureScope.ForLanguage(data.UiLanguage);
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
            logger.LogInformation(
                "Alice uploaded image 1 for chat {ChatId}: {ImageUrl}",
                data.ChatId,
                data.ImageUrl);

            if (data.Kind == AliceImageKind.Combining && !string.IsNullOrEmpty(data.Base64Image2))
            {
                data.ImageUrl2 = await animator.UploadImageFromBase64(data.Base64Image2, "image2.jpg");
                logger.LogInformation(
                    "Alice uploaded image 2 for chat {ChatId}: {ImageUrl}",
                    data.ChatId,
                    data.ImageUrl2);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to upload Alice studio image for chat {ChatId}", data.ChatId);
            data.ErrorMessage = PhotoAnimationWorkflowErrors.Failed;
        }

        return ExecutionResult.Next();
    }
}
