using GPTipsBot.Extensions;
using GPTipsBot.Localization;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace GPTipsBot.Services.YandexPhotoAnimator.Workflow.Steps;

public class DownloadPhotoStep(
    ITelegramBotClient botClient,
    PhotoAnimationProgressNotifier progressNotifier,
    ILogger<DownloadPhotoStep> logger) : StepBodyAsync
{
    public override async Task<ExecutionResult> RunAsync(IStepExecutionContext context)
    {
        var data = (PhotoAnimationWorkflowData)context.Workflow.Data;
        using var _ = UiCultureScope.ForLanguage(data.UiLanguage);
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
            logger.LogError(ex, "Failed to download photo for animation in chat {ChatId}", data.ChatId);
            data.ErrorMessage = PhotoAnimationWorkflowErrors.Failed;
        }

        return ExecutionResult.Next();
    }
}
