using Microsoft.Extensions.Hosting;
using dotenv.net;
using GPTipsBot.Extensions;
using GPTipsBot.Logging;
using Serilog;
using Prometheus;

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

await host.RunAsync();