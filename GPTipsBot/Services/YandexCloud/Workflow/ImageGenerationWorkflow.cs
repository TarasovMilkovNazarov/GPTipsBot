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
            .StartWith<GenerateImageStep>()
            .Then<NotifyUserStep>();
    }
}
