using Microsoft.Extensions.Logging;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace GPTipsBot.Services.YandexCloud.Workflow.Steps;

public class WaitForImageStep(
    IImageGenerator imageGenerator,
    ILogger<WaitForImageStep> logger) : StepBodyAsync
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(3);

    public string OperationId { get; set; } = string.Empty;
    public long ChatId { get; set; }
    public bool UseDevPlaceholder { get; set; }
    public string? ImageBase64 { get; set; }
    public string? ErrorMessage { get; set; }

    public override async Task<ExecutionResult> RunAsync(IStepExecutionContext context)
    {
        var data = (ImageGenerationWorkflowData)context.Workflow.Data;

        if (!string.IsNullOrEmpty(ErrorMessage) || UseDevPlaceholder)
        {
            return ExecutionResult.Next();
        }

        if (string.IsNullOrEmpty(OperationId))
        {
            ErrorMessage = ImageGenerationWorkflowErrors.Failed;
            return ExecutionResult.Next();
        }

        try
        {
            data.DeadlineUtc ??= DateTime.UtcNow.Add(Timeout);

            var status = await imageGenerator.GetImageGenerationStatusAsync(OperationId);
            if (status.Done)
            {
                if (string.IsNullOrEmpty(status.ImageBase64))
                {
                    ErrorMessage = ImageGenerationWorkflowErrors.Failed;
                }
                else
                {
                    ImageBase64 = status.ImageBase64;
                }

                return ExecutionResult.Next();
            }

            if (DateTime.UtcNow >= data.DeadlineUtc.Value)
            {
                logger.LogWarning("Image generation timed out for chat {ChatId}", ChatId);
                ErrorMessage = ImageGenerationWorkflowErrors.Failed;
                return ExecutionResult.Next();
            }

            return ExecutionResult.Sleep(PollInterval, data.DeadlineUtc);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed while waiting for image generation in chat {ChatId}", ChatId);
            ErrorMessage = ImageGenerationWorkflowErrors.Failed;
            return ExecutionResult.Next();
        }
    }
}
