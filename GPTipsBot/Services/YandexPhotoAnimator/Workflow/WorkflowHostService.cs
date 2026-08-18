using GPTipsBot.Services.YandexCloud.Workflow;
using Microsoft.Extensions.Hosting;
using WorkflowCore.Interface;

namespace GPTipsBot.Services.YandexPhotoAnimator.Workflow;

public class WorkflowHostService(IWorkflowHost workflowHost) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        workflowHost.RegisterWorkflow<PhotoAnimationWorkflow, PhotoAnimationWorkflowData>();
        workflowHost.RegisterWorkflow<AliceImageWorkflow, AliceImageWorkflowData>();
        workflowHost.RegisterWorkflow<ImageGenerationWorkflow, ImageGenerationWorkflowData>();
        workflowHost.Start();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        workflowHost.Stop();
        return Task.CompletedTask;
    }
}
