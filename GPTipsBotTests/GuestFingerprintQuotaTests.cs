using dotenv.net;
using GPTipsBot.Db;
using GPTipsBot.Extensions;
using GPTipsBot.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace GPTipsBotTests;

[TestFixture]
public class GuestFingerprintQuotaTests
{
    private IServiceProvider? _services;
    private bool _dbAvailable;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        DotEnv.Fluent().WithProbeForEnv(10).Load();
        Environment.SetEnvironmentVariable("ConnectionString",
            "Server=localhost;Port=5434;Database=gptips;User Id=postgres;Password=postgres;");
    }

    [SetUp]
    public async Task SetUp()
    {
        try
        {
            var services = new ServiceCollection().ConfigureServices();
            _services = services.BuildServiceProvider();
            var db = _services.GetRequiredService<ApplicationContext>();
            await db.GuestFingerprintQuotas.ExecuteDeleteAsync();
            _dbAvailable = true;
        }
        catch (Exception)
        {
            _dbAvailable = false;
            _services = null;
        }
    }

    private void RequireDb()
    {
        if (!_dbAvailable || _services is null)
        {
            Assert.Ignore("Postgres is not available on localhost:5434");
        }
    }

    [Test]
    public void NormalizeFingerprint_AcceptsVisitorIdShape()
    {
        Assert.That(WebUserService.NormalizeFingerprint("a1b2c3d4e5f67890"), Is.EqualTo("a1b2c3d4e5f67890"));
        Assert.That(WebUserService.NormalizeFingerprint("  AbCdEfGhIjKl  "), Is.EqualTo("AbCdEfGhIjKl"));
    }

    [Test]
    public void NormalizeFingerprint_RejectsInvalid()
    {
        Assert.That(WebUserService.NormalizeFingerprint(null), Is.Null);
        Assert.That(WebUserService.NormalizeFingerprint(""), Is.Null);
        Assert.That(WebUserService.NormalizeFingerprint("short"), Is.Null);
        Assert.That(WebUserService.NormalizeFingerprint("bad fingerprint!"), Is.Null);
        Assert.That(WebUserService.NormalizeFingerprint(new string('x', 200)), Is.Null);
    }

    [Test]
    public void HashIp_IsStableHex()
    {
        var a = WebUserService.HashIp("1.2.3.4");
        var b = WebUserService.HashIp("1.2.3.4");
        Assert.That(a, Is.EqualTo(b));
        Assert.That(a, Does.Match("^[0-9a-f]{64}$"));
        Assert.That(WebUserService.HashIp(null), Is.Null);
    }

    [Test]
    public async Task EnsureGuest_WithoutFingerprint_GetsNoFreeQuota()
    {
        RequireDb();
        var webUsers = _services!.GetRequiredService<WebUserService>();

        var (user, _) = await webUsers.EnsureGuestAsync("en", grantFreeQuota: true, fingerprint: null);
        Assert.That(user.FreeGptRequests, Is.EqualTo(0));
    }

    [Test]
    public async Task EnsureGuest_SameFingerprint_GetsFreeQuotaOnlyOnce()
    {
        RequireDb();
        var webUsers = _services!.GetRequiredService<WebUserService>();
        const string fp = "testfp00000001";

        var (first, _) = await webUsers.EnsureGuestAsync("en", true, fp, "10.0.0.1");
        var (second, _) = await webUsers.EnsureGuestAsync("en", true, fp, "10.0.0.1");

        Assert.That(first.FreeGptRequests, Is.EqualTo(WebUserService.GuestFreeGpt));
        Assert.That(second.FreeGptRequests, Is.EqualTo(0));
    }

    [Test]
    public async Task EnsureGuest_GrantFalse_SkipsEvenWithNewFingerprint()
    {
        RequireDb();
        var webUsers = _services!.GetRequiredService<WebUserService>();

        var (user, _) = await webUsers.EnsureGuestAsync(
            "en",
            grantFreeQuota: false,
            fingerprint: "testfp00000002",
            clientIp: "10.0.0.2");

        Assert.That(user.FreeGptRequests, Is.EqualTo(0));
        var db = _services.GetRequiredService<ApplicationContext>();
        Assert.That(await db.GuestFingerprintQuotas.AnyAsync(x => x.Fingerprint == "testfp00000002"), Is.False);
    }
}
