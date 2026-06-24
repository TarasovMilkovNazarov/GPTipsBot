using GPTipsBot.Extensions;
using Telegram.Bot;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace GPTipsBot.Services.YandexPhotoAnimator.Workflow.Steps;

public class DownloadPhotoStep(
    ITelegramBotClient botClient,
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
                await progressNotifier.ReportDownloadingPhotoAsync(data.ChatId, data.ProgressMessageId.Value);
            }

            data.Base64Image = await data.ImageFileId.GetPhotoAsync(botClient);
        }
        catch (Exception ex)
        {
            data.ErrorMessage = ex.Message;
        }

        return ExecutionResult.Next();
    }
}
