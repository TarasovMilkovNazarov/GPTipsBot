using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.OpenTelemetry;

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
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {SourceContext} {TraceId} {Message:lj}{NewLine}{Exception}");

            if (ctx.HostingEnvironment.IsProduction())
            {
                // VictoriaLogs accepts OTLP: http://victorialogs:9428/insert/opentelemetry/v1/logs.
                // Resource attributes become stream fields there, so logs split by service.
                var otlpUrl = ctx.Configuration["Logs:OtlpUrl"];
                if (string.IsNullOrWhiteSpace(otlpUrl))
                {
                    return;
                }

                loggerConfiguration
                    .WriteTo.OpenTelemetry(opts =>
                    {
                        // Logs only: without Endpoint = null the sink also exports traces to localhost:4317.
                        opts.Endpoint = null;
                        opts.LogsEndpoint = otlpUrl;
                        opts.Protocol = OtlpProtocol.HttpProtobuf;
                        opts.ResourceAttributes = new Dictionary<string, object>
                        {
                            ["service.name"] = appName,
                            ["deployment.environment"] = environment,
                        };
                    })
                    .WriteTo.Sink<TelegramSink>(LogEventLevel.Error);
            }
        });
}