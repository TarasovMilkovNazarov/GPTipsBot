using Ardalis.GuardClauses;
using Quartz;

namespace GPTipsBot.Jobs;

public interface IJobService
{
    Task<DateTimeOffset?> GetNextExecutionForExistingJob<T>() where T : IJob;
}

public class JobService : IJobService
{
    private readonly ISchedulerFactory _factory;

    public JobService(ISchedulerFactory factory)
    {
        _factory = factory;
    }

    public async Task<DateTimeOffset?> GetNextExecutionForExistingJob<T>() where T : IJob
    {
        var scheduler = await _factory.GetScheduler();
        var fullName = typeof(T).FullName;
        Guard.Against.Null(fullName);

        var jobKey = new JobKey(fullName);

        var triggerKey = new TriggerKey($"{jobKey.Name}Trigger");
        var trigger = await scheduler.GetTrigger(triggerKey);

        var nextFireTime = trigger?.GetNextFireTimeUtc();
        return nextFireTime;
    }
}