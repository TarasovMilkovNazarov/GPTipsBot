using Elastic.Channels;
using Elastic.Ingest.Elasticsearch;
using Elastic.Ingest.Elasticsearch.DataStreams;
using Elastic.Serilog.Sinks;
using Elastic.Transport;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

namespace GPTipsBot.Logging;

public static class HostBuilderForLogExtensions
{
    // ReSharper disable once UnusedMethodReturnValue.Global
    public static IHostBuilder SetupGpTipsLog(this IHostBuilder builder) =>
        builder.UseSerilog((ctx, _, loggerConfiguration) =>
        {
            var appName = ctx.HostingEnvironment.ApplicationName;
            var environment = ctx.HostingEnvironment.EnvironmentName;
    
            loggerConfiguration
                .ReadFrom.Configuration(ctx.Configuration)
                .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Error)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("service.name", appName)
                .Enrich.WithProperty("service.environment", environment)
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {SourceContext} {TraceId} {Message:lj}{NewLine}{Exception}");
    
            if (ctx.HostingEnvironment.IsProduction())
            {
                var elasticUrl = ctx.Configuration["Elastic:Url"] ?? throw new InvalidOperationException("Elastic:Url не задан");
                var esApiKey = ctx.Configuration["Elastic:ApiKey"] ?? throw new InvalidOperationException("Elastic:ApiKey не задан");
                
                loggerConfiguration
                    .WriteTo.Elasticsearch(
                        [new Uri(elasticUrl)],
                        opts =>
                        {
                            opts.DataStream = new DataStreamName("logs", appName, environment);
                            opts.BootstrapMethod = BootstrapMethod.Failure;
                            opts.ConfigureChannel = ch =>
                            {
                                ch.BufferOptions = new BufferOptions
                                {
                                    OutboundBufferMaxSize = 1000,
                                    ExportMaxConcurrency = 1
                                };
                            };
                        },
                        transport => { transport.Authentication(new ApiKey(esApiKey)); })
                    .WriteTo.Sink<TelegramSink>(LogEventLevel.Error);;
            }
        });
}