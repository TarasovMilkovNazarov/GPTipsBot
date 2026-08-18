using WorkflowCore.Interface;

namespace GPTipsBot.Services.YandexPhotoAnimator.Workflow;

public class AliceImageWorkflowService(IWorkflowHost workflowHost)
{
    public Task<string> StartAsync(AliceImageWorkflowData data)
        => workflowHost.StartWorkflow(nameof(AliceImageWorkflow), 1, data);
}
