using Microsoft.Extensions.Hosting;
using dotenv.net;
using GPTipsBot.Extensions;
using GPTipsBot.Jobs;
using GPTipsBot.Logging;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Serilog;

//uncomment for sniffing requests in fiddler
//ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;

//var metricServer = new MetricServer(port: 5001);
//metricServer.Start();
DotEnv.Fluent().WithProbeForEnv(10).Load();

Log.Logger = new LoggerConfiguration()
    .AddBotLogger()
    .CreateBootstrapLogger();

var host = Host.CreateDefaultBuilder(args)
    .UseSerilog((context, services, configuration) => configuration.AddBotLogger(context.Configuration, services))
    .ConfigureServices((_, services) => services.ConfigureServices())
    .Build();

var schedulerFactory = host.Services.GetRequiredService<ISchedulerFactory>();
var scheduler = await schedulerFactory.GetScheduler();
var schedulerService = host.Services.GetRequiredService<ISchedulerService>();

// todo добавить бд для джоб
await schedulerService.ScheduleJob<DailyStatisticsJob>(scheduler, DateBuilder.TodayAt(23, 55, 0),
    TimeSpan.FromDays(1),  CancellationToken.None);
await schedulerService.ScheduleJob<RefreshFreeLimitsJob>(scheduler, DateBuilder.TodayAt(23, 59, 0),
    TimeSpan.FromDays(2),  CancellationToken.None);
await schedulerService.ScheduleJob<RemoveOldRecordsJob>(scheduler, DateBuilder.FutureDate(14, IntervalUnit.Day),
    TimeSpan.FromDays(14),  CancellationToken.None);
await schedulerService.ScheduleJob<DeactivateKickedUsersJob>(scheduler, DateBuilder.FutureDate(5, IntervalUnit.Day),
    TimeSpan.FromDays(5),  CancellationToken.None);
await scheduler.Start();

await host.RunAsync();
