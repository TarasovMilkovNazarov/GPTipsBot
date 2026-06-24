using WorkflowCore.Interface;

namespace GPTipsBot.Services.YandexPhotoAnimator.Workflow;

public class PhotoAnimationWorkflowService(IWorkflowHost workflowHost)
{
    public Task<string> StartAsync(PhotoAnimationWorkflowData data)
        => workflowHost.StartWorkflow(nameof(PhotoAnimationWorkflow), 1, data);
}
