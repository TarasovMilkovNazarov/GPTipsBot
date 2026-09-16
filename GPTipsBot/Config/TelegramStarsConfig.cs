namespace GPTipsBot.Config;

/// <summary>
/// Telegram Stars (XTR) rail.
/// <para>
/// Telegram pays the bot out ~$0.013 per XTR (~$0.009 when the user bought the stars inside the iOS /
/// Android app, where Apple and Google take 30%), minus Fragment's withdrawal fee — roughly 1 ₽ per XTR.
/// One ruble is <see cref="YooKassaConfig.GemsPerRub"/> gems, so a star buys the same
/// <see cref="GemsPerXtr"/> gems that a ruble does.
/// </para>
/// <para>
/// The rate is declared here rather than implied: the wallet used to credit XTR one-for-one against a
/// unit worth 10 ₽, which sold balance about ten times below cost. A gem is an abstract unit with one
/// published rate per rail, so a payout change is a config edit, not a repricing of every feature.
/// </para>
/// </summary>
public static class TelegramStarsConfig
{
    private const int DefaultGemsPerXtr = 20;

    /// <summary>Telegram rejects invoices priced above this many stars.</summary>
    public const int MaxXtrPerInvoice = 10_000;

    /// <summary>Gems credited per 1 XTR paid. Override with <c>TELEGRAM_GEMS_PER_XTR</c>.</summary>
    public static int GemsPerXtr
    {
        get
        {
            var raw = Environment.GetEnvironmentVariable("TELEGRAM_GEMS_PER_XTR")?.Trim().Trim('"', '\'');
            return int.TryParse(raw, out var value) && value > 0 ? value : DefaultGemsPerXtr;
        }
    }

    /// <summary>Largest deposit that still fits into a single Telegram invoice.</summary>
    public static long MaxGemsPerInvoice => (long)MaxXtrPerInvoice * GemsPerXtr;

    /// <summary>
    /// XTR the user has to pay for <paramref name="gems"/> gems. A star cannot be split, so an amount
    /// that is not a whole number of stars rounds up — never down, which would hand out free gems.
    /// Every offered package is a multiple of <see cref="GemsPerXtr"/>, so rounding only ever affects
    /// a hand-typed amount, and by less than one star.
    /// </summary>
    public static int GemsToXtr(long gems) =>
        checked((int)((gems + GemsPerXtr - 1) / GemsPerXtr));
}
