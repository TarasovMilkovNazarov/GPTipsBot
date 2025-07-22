using System.Net.Http.Json;
using GPTipsBot.Db;
using GPTipsBot.Enums;
using GPTipsBot.Models;
using GPTipsBot.Repositories;
using Microsoft.EntityFrameworkCore;
using Quartz;
using Telegram.Bot;
using Telegram.Bot.Exceptions;

namespace GPTipsBot.Dtos;

public class ConsumePendingOperationJob : IJob
{
    private readonly ApplicationContext _context;
    private readonly ITelegramBotClient _botClient;
    private readonly IGenericRepository<PendingOperation> _pendingOperationRepository;
    private readonly HttpClient _httpClient;

    public ConsumePendingOperationJob(ApplicationContext context, ITelegramBotClient botClient,
        IGenericRepository<PendingOperation> pendingOperationRepository, HttpClient httpClient)
    {
        _context = context;
        _botClient = botClient;
        _pendingOperationRepository = pendingOperationRepository;
        _httpClient = httpClient;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var operations = _pendingOperationRepository.Get(op => op.Status == OperationStatus.InProgress &&
                                                               op.Type == OperationType.Video).ToList();

        foreach (var op in operations)
        {
            var getStatusRequest = new HttpRequestMessage(HttpMethod.Get, new Uri(_httpClient.BaseAddress, $"video/status?request_id={op.Data}"))
            {
                Headers =
                {
                    {"Authorization", $"Key {AppConfig.ProxyApiApiKey}"},
                }
            };
            var response = await _httpClient.SendAsync(getStatusRequest, context.CancellationToken);

            var generateResult = await response.Content.ReadFromJsonAsync<GenerateResponse>
                (cancellationToken: context.CancellationToken);


            var isGenerated = false;
            switch (generateResult?.Status)
            {
                case "COMPLETED":
                    isGenerated = true;
                    break;
                case "FAILED":
                    op.Status = OperationStatus.Failed;
                    break;
                default:
                    isGenerated = false;
                    break;
            }

            if (isGenerated)
            {
                var url = generateResult?.Url;
                op.Status = OperationStatus.Completed;
                break;
            }
        }

        await _context.SaveChangesAsync();
    }
}