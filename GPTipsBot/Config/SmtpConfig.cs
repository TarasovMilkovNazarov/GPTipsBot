namespace GPTipsBot.Config;

public static class SmtpConfig
{
    public static bool IsEnabled =>
        !string.IsNullOrWhiteSpace(Host) &&
        !string.IsNullOrWhiteSpace(From) &&
        Port > 0;

    public static string? Host => Normalize(Environment.GetEnvironmentVariable("SMTP_HOST"));
    public static int Port =>
        int.TryParse(Normalize(Environment.GetEnvironmentVariable("SMTP_PORT")), out var port) && port > 0
            ? port
            : 587;
    public static string? User => Normalize(Environment.GetEnvironmentVariable("SMTP_USER"));
    public static string? Password => Normalize(Environment.GetEnvironmentVariable("SMTP_PASSWORD"));
    public static string? From => Normalize(Environment.GetEnvironmentVariable("SMTP_FROM")) ?? User;
    public static bool UseSsl =>
        !string.Equals(
            Normalize(Environment.GetEnvironmentVariable("SMTP_SSL")),
            "false",
            StringComparison.OrdinalIgnoreCase);

    private static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim().Trim('"', '\'');
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}
