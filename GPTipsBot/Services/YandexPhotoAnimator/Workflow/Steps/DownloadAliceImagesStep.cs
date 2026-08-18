using GPTipsBot.Extensions;
using GPTipsBot.Localization;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace GPTipsBot.Services.YandexPhotoAnimator.Workflow.Steps;

public class DownloadAliceImagesStep(
    ITelegramBotClient botClient,
    PhotoAnimationProgressNotifier progressNotifier,
    ILogger<DownloadAliceImagesStep> logger) : StepBodyAsync
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
                await progressNotifier.ReportDownloadingPhotoAsync(data.ChatId, data.ProgressMessageId.Value);
            }

            data.Base64Image = await data.ImageFileId.GetPhotoAsync(botClient);
            if (data.Kind == AliceImageKind.Combining && !string.IsNullOrEmpty(data.ImageFileId2))
            {
                data.Base64Image2 = await data.ImageFileId2.GetPhotoAsync(botClient);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to download Alice studio photo for chat {ChatId}", data.ChatId);
            data.ErrorMessage = PhotoAnimationWorkflowErrors.Failed;
        }

        return ExecutionResult.Next();
    }
}
