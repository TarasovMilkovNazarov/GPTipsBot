using dotenv.net;
using GPTipsBot.Extensions;
using GPTipsBot.Jobs;
using GPTipsBot.Logging;
using GPTipsBot.Web;
using GPTipsBot.Webhooks;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Quartz;

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

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .SetIsOriginAllowed(_ => true));
});

builder.Services
    .AddAuthentication(WebAuthConstants.Scheme)
    .AddCookie(WebAuthConstants.Scheme, options =>
    {
        options.Cookie.Name = WebAuthConstants.CookieName;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.Events = new CookieAuthenticationEvents
        {
            OnRedirectToLogin = ctx =>
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            },
            OnRedirectToAccessDenied = ctx =>
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            },
        };
    });
builder.Services.AddAuthorization();

builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 25 * 1024 * 1024;
});
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 25 * 1024 * 1024;
});

var app = builder.Build();
app.UseForwardedHeaders();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

var webRoot = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
if (Directory.Exists(webRoot))
{
    app.UseDefaultFiles(new DefaultFilesOptions
    {
        FileProvider = new PhysicalFileProvider(webRoot),
    });
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(webRoot),
    });
}

app.MapYooKassaWebhook();
app.MapWebApi();
app.MapSeoFiles();

if (Directory.Exists(webRoot))
{
    app.MapFallbackToFile("index.html", new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(webRoot),
    });
}
else
{
    app.MapGet("/", () => Results.Ok(new
    {
        name = "GPTipsBot",
        web = "Build the web UI (see /web) and copy dist into GPTipsBot/wwwroot",
        health = "/health",
        api = "/api",
    }));
}

var schedulerFactory = app.Services.GetRequiredService<ISchedulerFactory>();
var scheduler = await schedulerFactory.GetScheduler();
var schedulerService = app.Services.GetRequiredService<ISchedulerService>();

await schedulerService.ScheduleJob<DailyStatisticsJob>(scheduler, DateBuilder.TodayAt(23, 55, 0),
    TimeSpan.FromDays(1), CancellationToken.None);
await schedulerService.ScheduleJob<RefreshFreeLimitsJob>(scheduler, DateBuilder.TodayAt(23, 59, 0),
    TimeSpan.FromDays(1), CancellationToken.None);
await schedulerService.ScheduleJob<RemoveOldRecordsJob>(scheduler, DateBuilder.FutureDate(14, IntervalUnit.Day),
    TimeSpan.FromDays(14), CancellationToken.None);
await schedulerService.ScheduleJob<DeactivateKickedUsersJob>(scheduler, DateBuilder.FutureDate(5, IntervalUnit.Day),
    TimeSpan.FromDays(5), CancellationToken.None);
await schedulerService.ScheduleJob<SyncYooKassaPaymentsJob>(scheduler, DateBuilder.FutureDate(1, IntervalUnit.Minute),
    TimeSpan.FromMinutes(2), CancellationToken.None);
await schedulerService.ScheduleJob<ReleaseExpiredPaymentHoldsJob>(scheduler, DateBuilder.FutureDate(5, IntervalUnit.Minute),
    TimeSpan.FromMinutes(5), CancellationToken.None);
await schedulerService.ScheduleJob<YandexCloudBalanceAlertJob>(scheduler, DateBuilder.FutureDate(1, IntervalUnit.Minute),
    TimeSpan.FromMinutes(15), CancellationToken.None);

await app.RunAsync();
