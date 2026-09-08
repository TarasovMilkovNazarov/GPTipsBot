using GPTipsBot.Config;
using GPTipsBot.Services.YandexCloud;
using Microsoft.Extensions.Logging;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace GPTipsBot.Services.YandexCloud.Workflow.Steps;

/// <summary>
/// Generates the image in a single synchronous call to the Images API,
/// replacing the StartGenerateImageStep + WaitForImageStep pair.
/// </summary>
public class GenerateImageStep(
    IImageGenerator imageGenerator,
    ILogger<GenerateImageStep> logger) : StepBodyAsync
{
    public override async Task<ExecutionResult> RunAsync(IStepExecutionContext context)
    {
        var data = (ImageGenerationWorkflowData)context.Workflow.Data;
        if (!string.IsNullOrEmpty(data.ErrorMessage))
        {
            return ExecutionResult.Next();
        }

        try
        {
            if (!AppConfig.IsProduction)
            {
                data.UseDevPlaceholder = true;
                return ExecutionResult.Next();
            }

            data.ImageBase64 = await imageGenerator.GenerateImageAsync(data.Prompt, data.IsSquare);
        }
        catch (YandexArtRejectedException ex)
        {
            logger.LogWarning(ex, "Images API rejected image prompt for chat {ChatId}", data.ChatId);
            data.ErrorMessage = ImageGenerationWorkflowErrors.Rejected;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate image for chat {ChatId}", data.ChatId);
            data.ErrorMessage = ImageGenerationWorkflowErrors.Failed;
        }

        return ExecutionResult.Next();
    }
}
