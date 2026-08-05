using dotenv.net;
using GPTipsBot.Extensions;
using GPTipsBot.Jobs;
using GPTipsBot.Logging;
using GPTipsBot.Webhooks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

//uncomment for sniffing requests in fiddler
//ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;

//var metricServer = new MetricServer(port: 5001);
//metricServer.Start();
DotEnv.Fluent().WithProbeForEnv(10).Load();

var builder = WebApplication.CreateBuilder(args);
builder.Host.SetupGpTipsLog();
builder.Services.ConfigureServices();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();
app.UseForwardedHeaders();
app.MapYooKassaWebhook();

var schedulerFactory = app.Services.GetRequiredService<ISchedulerFactory>();
var scheduler = await schedulerFactory.GetScheduler();
var schedulerService = app.Services.GetRequiredService<ISchedulerService>();

await schedulerService.ScheduleJob<DailyStatisticsJob>(scheduler, DateBuilder.TodayAt(23, 55, 0),
    TimeSpan.FromDays(1),  CancellationToken.None);
await schedulerService.ScheduleJob<RefreshFreeLimitsJob>(scheduler, DateBuilder.TodayAt(23, 59, 0),
    TimeSpan.FromDays(1),  CancellationToken.None);
await schedulerService.ScheduleJob<RemoveOldRecordsJob>(scheduler, DateBuilder.FutureDate(14, IntervalUnit.Day),
    TimeSpan.FromDays(14),  CancellationToken.None);
await schedulerService.ScheduleJob<DeactivateKickedUsersJob>(scheduler, DateBuilder.FutureDate(5, IntervalUnit.Day),
    TimeSpan.FromDays(5),  CancellationToken.None);
await schedulerService.ScheduleJob<SyncYooKassaPaymentsJob>(scheduler, DateBuilder.FutureDate(1, IntervalUnit.Minute),
    TimeSpan.FromMinutes(2), CancellationToken.None);
await scheduler.Start();

await app.RunAsync();
