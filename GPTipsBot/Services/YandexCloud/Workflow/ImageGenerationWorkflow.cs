using GPTipsBot.Services.YandexCloud.Workflow.Steps;
using WorkflowCore.Interface;

namespace GPTipsBot.Services.YandexCloud.Workflow;

public class ImageGenerationWorkflow : IWorkflow<ImageGenerationWorkflowData>
{
    public string Id => nameof(ImageGenerationWorkflow);

    public int Version => 1;

    public void Build(IWorkflowBuilder<ImageGenerationWorkflowData> builder)
    {
        builder
            .StartWith<StartGenerateImageStep>()
            .Then<WaitForImageStep>()
                .Input(step => step.OperationId, data => data.OperationId ?? string.Empty)
                .Input(step => step.ChatId, data => data.ChatId)
                .Input(step => step.UseDevPlaceholder, data => data.UseDevPlaceholder)
                .Input(step => step.ErrorMessage, data => data.ErrorMessage)
                .Output(data => data.ImageBase64, step => step.ImageBase64)
                .Output(data => data.ErrorMessage, step => step.ErrorMessage)
            .Then<NotifyUserStep>();
    }
}
