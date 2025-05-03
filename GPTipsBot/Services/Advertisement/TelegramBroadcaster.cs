using GPTipsBot.Repositories;
using GPTipsBot.Resources;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace GPTipsBot.Services.Advertisement;

public class TelegramBroadcaster: BackgroundService
{
    private readonly ITelegramBotClient _botClient;
    private readonly UserRepository userRepository;
    private readonly ILogger<TelegramBroadcaster> _logger;

    public TelegramBroadcaster(ITelegramBotClient botClient, UserRepository userRepository, ILogger<TelegramBroadcaster> logger)
    {
        _botClient = botClient;
        this.userRepository = userRepository;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var userIds = userRepository.GetAllIdentificators();

        foreach (var userId in userIds)
        {
            try
            {
                await _botClient.SendTextMessageAsync(userId, BotResponse.Hamster, null, ParseMode.MarkdownV2,
                    cancellationToken: stoppingToken);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to send vpn advertisement message for user={userId}", userId);
            }

            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }
}