using WorkflowCore.Interface;

namespace GPTipsBot.Services.YandexCloud.Workflow;

public class ImageGenerationWorkflowService(IWorkflowHost workflowHost)
{
    public Task<string> StartAsync(ImageGenerationWorkflowData data)
        => workflowHost.StartWorkflow(nameof(ImageGenerationWorkflow), 1, data);
}
