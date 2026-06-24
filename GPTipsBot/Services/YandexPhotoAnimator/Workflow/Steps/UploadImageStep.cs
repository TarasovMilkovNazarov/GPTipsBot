using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace GPTipsBot.Services.YandexPhotoAnimator.Workflow.Steps;

public class UploadImageStep(
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
                await progressNotifier.ReportUploadingImageAsync(data.ChatId, data.ProgressMessageId.Value);
            }

            data.ImageUrl = await animator.UploadImageFromBase64(data.Base64Image!);
        }
        catch (Exception ex)
        {
            data.ErrorMessage = ex.Message;
        }

        return ExecutionResult.Next();
    }
}
