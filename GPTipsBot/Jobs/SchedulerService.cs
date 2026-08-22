using Quartz;

namespace GPTipsBot.Jobs;

public interface ISchedulerService
{
    Task ScheduleJob<T>(IScheduler scheduler, DateTimeOffset startAt,
        TimeSpan interval, CancellationToken cancellationToken)
        where T : IJob;
}

public class SchedulerService : ISchedulerService
{
    public async Task ScheduleJob<T>(IScheduler scheduler, DateTimeOffset startAt,
        TimeSpan interval, CancellationToken cancellationToken)
        where T : IJob
    {
        var jobKey = new JobKey(typeof(T).FullName);

        var triggerKey = new TriggerKey($"{jobKey.Name}Trigger");
        
        if (await scheduler.CheckExists(jobKey, cancellationToken))
        {
            return;
        }

        var job = JobBuilder.Create<T>()
            .WithIdentity(jobKey)
            .Build();

        var trigger = TriggerBuilder.Create()
            .WithIdentity(triggerKey)
            .WithSimpleSchedule(x => x.WithInterval(interval)
                .RepeatForever())
            .StartAt(startAt)
            // .StartNow()
            .Build();

        await scheduler.ScheduleJob(job, trigger, cancellationToken);
    }
}