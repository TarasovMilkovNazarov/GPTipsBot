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
        const int batchSize = 15;
        var userIds = userRepository.GetAllIdentificators();
        var batches = userIds.Select((userId, index) => new { userId, index })
            .GroupBy(x => x.index / batchSize)
            .Select(g => g.Select(x => x.userId).ToList())
            .ToList();

        var failedToSendUserIds = new List<long>();

        foreach (var batch in batches)
        {
            var sendTasks = batch.Select(async userId =>
            {
                try
                {
                    await _botClient.SendTextMessageAsync(userId, BotResponse.Hamster, null, ParseMode.MarkdownV2,
                        cancellationToken: stoppingToken);
                }
                catch (Exception e)
                {
                    failedToSendUserIds.Add(userId);
                    // _logger.LogError(e, "Failed to send vpn advertisement message for user={userId}", userId);
                }
            });

            await Task.WhenAll(sendTasks);
            await Task.Delay(TimeSpan.FromMilliseconds(300), stoppingToken);
        }

        _logger.LogInformation("Failed to send vpn advertisement message for users={userId}", String.Join(", ", failedToSendUserIds));
    }
}