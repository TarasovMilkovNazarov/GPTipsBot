using Microsoft.Extensions.Hosting;
using dotenv.net;
using GPTipsBot.Dtos;
using GPTipsBot.Extensions;
using GPTipsBot.Logging;
using GPTipsBot.Services;
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
// await schedulerService.ScheduleJob<DailyJob>(scheduler, TimeSpan.FromDays(1),  CancellationToken.None);
// await schedulerService.ScheduleJob<BotUpdateInformerJob>(scheduler, TimeSpan.FromDays(1),  CancellationToken.None);
await scheduler.Start();

await host.RunAsync();
