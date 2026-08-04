using GPTipsBot.Config;
using Microsoft.Extensions.Logging;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace GPTipsBot.Services.YandexCloud.Workflow.Steps;

public class StartGenerateImageStep(
    IImageGenerator imageGenerator,
    ILogger<StartGenerateImageStep> logger) : StepBodyAsync
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

            data.OperationId = await imageGenerator.StartImageGenerationAsync(data.Prompt, data.IsSquare);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start image generation for chat {ChatId}", data.ChatId);
            data.ErrorMessage = ImageGenerationWorkflowErrors.Failed;
        }

        return ExecutionResult.Next();
    }
}
