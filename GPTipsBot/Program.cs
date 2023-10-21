using Microsoft.Extensions.Hosting;
using dotenv.net;
using GPTipsBot.Extensions;

DotEnv.Fluent().WithProbeForEnv(10).Load();

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((_, services) => { services.ConfigureServices(); })
    .Build();

await host.RunAsync();