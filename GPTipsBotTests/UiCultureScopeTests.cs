using System.Globalization;
using GPTipsBot.Localization;
using GPTipsBot.Resources;
using NUnit.Framework;

namespace GPTipsBotTests;

[TestFixture]
public class UiCultureScopeTests
{
    [Test]
    public async Task ParallelFlows_DoNotTakeLastRequestLanguage()
    {
        var seen = new string[2];
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var arabic = Task.Run(async () =>
        {
            using var scope = new UiCultureScope(LocalizationManager.Ar);
            started.TrySetResult();
            await release.Task;
            await Task.Yield();
            seen[0] = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        });

        var russian = Task.Run(async () =>
        {
            await started.Task;
            using var scope = new UiCultureScope(LocalizationManager.Ru);
            release.TrySetResult();
            await Task.Yield();
            seen[1] = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        });

        await Task.WhenAll(arabic, russian);

        Assert.That(seen[0], Is.EqualTo("ar"));
        Assert.That(seen[1], Is.EqualTo("ru"));
    }

    [Test]
    public void Dispose_RestoresPreviousCulture()
    {
        CultureInfo.CurrentUICulture = LocalizationManager.Ru;
        using (new UiCultureScope(LocalizationManager.Ar))
        {
            Assert.That(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, Is.EqualTo("ar"));
        }

        Assert.That(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, Is.EqualTo("ru"));
    }

    [Test]
    public void ForLanguage_EmptyFallsBackToRu()
    {
        CultureInfo.CurrentUICulture = LocalizationManager.En;
        using var scope = UiCultureScope.ForLanguage(null);
        Assert.That(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, Is.EqualTo("ru"));
    }

    [Test]
    public void ForLanguage_AppliesBotResponseLanguage()
    {
        CultureInfo.CurrentUICulture = LocalizationManager.Ru;
        using (UiCultureScope.ForLanguage("es"))
        {
            Assert.That(BotResponse.SomethingWentWrong, Is.EqualTo("Algo salió mal, inténtalo de nuevo."));
        }

        Assert.That(BotResponse.SomethingWentWrong, Is.EqualTo("Что-то пошло не так, попробуйте ещё раз"));
    }
}
