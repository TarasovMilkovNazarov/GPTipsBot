namespace GPTipsBot.Services.YandexCloud;

/// <summary>
/// YandexART refused to generate (content policy / unsupported topic). Not a system failure.
/// </summary>
public sealed class YandexArtRejectedException(int? code, string? providerMessage)
    : Exception(providerMessage ?? "YandexART rejected the prompt")
{
    public int? Code { get; } = code;
}
