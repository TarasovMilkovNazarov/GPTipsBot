using GPTipsBot.Localization;
using Microsoft.Extensions.Logging;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace GPTipsBot.Services.YandexPhotoAnimator.Workflow.Steps;

public class GenerateAliceImageStep(
    YaPhotoAnimatorService animator,
    PhotoAnimationProgressNotifier progressNotifier,
    ILogger<GenerateAliceImageStep> logger) : StepBodyAsync
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
                await progressNotifier.ReportStartingImageGenerationAsync(data.ChatId, data.ProgressMessageId.Value);
            }

            AliceImageGenerationResult? response;
            if (data.Kind == AliceImageKind.Combining)
            {
                if (string.IsNullOrEmpty(data.ImageUrl) || string.IsNullOrEmpty(data.ImageUrl2))
                {
                    logger.LogWarning("Alice combining is missing uploaded URLs for chat {ChatId}", data.ChatId);
                    data.ErrorMessage = PhotoAnimationWorkflowErrors.Failed;
                    return ExecutionResult.Next();
                }

                response = await animator.CombineImages(data.ImageUrl, data.ImageUrl2, data.Prompt);
            }
            else
            {
                if (string.IsNullOrEmpty(data.ImageUrl))
                {
                    logger.LogWarning("Alice editing is missing uploaded URL for chat {ChatId}", data.ChatId);
                    data.ErrorMessage = PhotoAnimationWorkflowErrors.Failed;
                    return ExecutionResult.Next();
                }

                response = await animator.EditImage(data.ImageUrl, data.Prompt);
            }

            if (string.IsNullOrEmpty(response?.Id))
            {
                logger.LogWarning(
                    "Alice {Kind} generation was not started for chat {ChatId}. PromptLength={PromptLength}, HasUrl1={HasUrl1}, HasUrl2={HasUrl2}, ParsedStatus={ParsedStatus}",
                    data.Kind,
                    data.ChatId,
                    data.Prompt?.Length ?? 0,
                    !string.IsNullOrEmpty(data.ImageUrl),
                    !string.IsNullOrEmpty(data.ImageUrl2),
                    response?.Status);
                data.ErrorMessage = PhotoAnimationWorkflowErrors.Failed;
                return ExecutionResult.Next();
            }

            data.GenerationId = response.Id;
            if (!string.IsNullOrEmpty(response.ImageUrl))
            {
                data.ResultImageUrl = response.ImageUrl;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start Alice {Kind} for chat {ChatId}", data.Kind, data.ChatId);
            data.ErrorMessage = PhotoAnimationWorkflowErrors.Failed;
        }

        return ExecutionResult.Next();
    }
}
