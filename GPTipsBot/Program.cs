using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using dotenv.net;
using GPTipsBot.Extensions;

DotEnv.Fluent().WithProbeForEnv(10).Load();

var host = HostBuilder.CreateHostBuilder(args).Build();

await host.RunAsync();

public class HostBuilder
{
    public static IHostBuilder CreateHostBuilder(string[] args)
    {
        return Host.CreateDefaultBuilder(args)
        // Add configuration, logging, ...
        .ConfigureServices((hostContext, services) =>
        {
            services.ConfigureServices();
        });
    }
}

