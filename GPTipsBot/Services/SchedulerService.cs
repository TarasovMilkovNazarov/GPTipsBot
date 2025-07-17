using Quartz;

namespace GPTipsBot.Services;

public interface ISchedulerService
{
    Task ScheduleJob<T>(IScheduler scheduler, TimeSpan interval, CancellationToken cancellationToken)
        where T : IJob;
}

public class SchedulerService : ISchedulerService
{
    private readonly IServiceProvider _serviceProvider;

    public SchedulerService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }


    public async Task ScheduleJob<T>(IScheduler scheduler, TimeSpan interval, CancellationToken cancellationToken)
        where T : IJob
    {
        var jobKey = new JobKey(typeof(T).FullName);

        var triggerKey = new TriggerKey($"{jobKey.Name}Trigger");
        
        if (await scheduler.CheckExists(jobKey, cancellationToken))
        {
            await scheduler.DeleteJob(jobKey, cancellationToken);
        }

        var job = JobBuilder.Create<T>()
            .WithIdentity(jobKey)
            .Build();

        var trigger = TriggerBuilder.Create()
            .WithIdentity(triggerKey)
            .StartAt(DateBuilder.TodayAt(23, 59, 0))
            .WithSimpleSchedule(x => x.WithInterval(interval)
                .RepeatForever())
            .Build();

        await scheduler.ScheduleJob(job, trigger, cancellationToken);
    }
}