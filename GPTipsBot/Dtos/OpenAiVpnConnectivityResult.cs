namespace GPTipsBot.Dtos;

public record OpenAiVpnConnectivityResult(
    bool IsSuccess,
    string ProxyEndpoint,
    string? ErrorMessage,
    TimeSpan Duration);
