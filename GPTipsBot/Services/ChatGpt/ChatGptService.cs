using Ardalis.GuardClauses;
using GPTipsBot.Config;
using GPTipsBot.Dtos;
using GPTipsBot.Exceptions;
using GPTipsBot.Repositories;
using GPTipsBot.Services.YandexPhotoAnimator.Workflow;
using Microsoft.Extensions.Logging;
using OpenAI.Interfaces;
using OpenAI.ObjectModels.RequestModels;
using OpenAI.ObjectModels.ResponseModels;
using Polly;
using Polly.Retry;

namespace GPTipsBot.Services
{
    public class ChatGptService : IGpt
    {
        private readonly ILogger<ChatGptService> _log;
        private readonly IOpenAIService _openAiService;
        private readonly ContextWindow _contextWindow;
        private readonly PhotoAnimationWorkflowService _photoAnimationWorkflowService;
        private readonly BotSettingsRepository _botSettingsRepository;
        private readonly AsyncRetryPolicy _policy;

        private const int MaxRetryCount = 4;

        public ChatGptService(
            ILogger<ChatGptService> log,
            IOpenAIService openAiService,
            ContextWindow contextWindow,
            PhotoAnimationWorkflowService photoAnimationWorkflowService,
            BotSettingsRepository botSettingsRepository)
        {
            _log = log;
            _openAiService = openAiService;
            _contextWindow = contextWindow;
            _photoAnimationWorkflowService = photoAnimationWorkflowService;
            _botSettingsRepository = botSettingsRepository;
            _policy = Policy
                .Handle<ChatGptException>()
                .WaitAndRetryAsync(MaxRetryCount, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
        }

        public async Task<ChatCompletionCreateResponse> SendMessage(UpdateDecorator update, CancellationToken token)
        {
            ChatMessage[] textWithContext;

            if (update.Message.NewContext)
            {
                textWithContext = [new ChatMessage(update.Message.Role.ToString().ToLower(), update.Message.Text)];
            }
            else
            {
                textWithContext = _contextWindow.GetContext(update.UserChatKey, update.Message.ContextId.Value);
            }

            var modelId = ResolveModelId(update.UserChatKey.Id);
            return await SendMessageInternal(textWithContext, modelId, token);
        }

        public Task<ChatCompletionCreateResponse> SendOneOffAsync(
            string systemPrompt,
            string userPrompt,
            CancellationToken token,
            string? modelId = null)
        {
            ChatMessage[] messages =
            [
                new ChatMessage("system", systemPrompt),
                new ChatMessage("user", userPrompt)
            ];

            return SendMessageInternal(messages, modelId ?? GptModelCatalog.DefaultModelId, token)!;
        }

        public Task StartAnimatePhoto(
            string prompt,
            string imageFileId,
            long chatId,
            long userId,
            int progressMessageId,
            long paymentHoldId)
        {
            var workflowData = new PhotoAnimationWorkflowData
            {
                ChatId = chatId,
                UserId = userId,
                ImageFileId = imageFileId,
                Prompt = prompt,
                ProgressMessageId = progressMessageId,
                PaymentHoldId = paymentHoldId,
            };

            return _photoAnimationWorkflowService.StartAsync(workflowData);
        }

        private string ResolveModelId(long userId) =>
            GptModelCatalog.Resolve(_botSettingsRepository.Get(userId)?.PreferredGptModel).Id;

        private async Task<ChatCompletionCreateResponse?> SendMessageInternal(
            ChatMessage[] messages,
            string modelId,
            CancellationToken cancellationToken)
        {
            _log.LogInformation(
                "Send request to OpenAi service model={ModelId}: {messages}",
                modelId,
                messages.Last().Content);

            ChatCompletionCreateResponse? response = null;

            await _policy.ExecuteAsync(async (context, _) =>
            {
                var retryAttempt = context.TryGetValue("retryAttempt", out var value)
                    ? (int)value : 0;

                try
                {
                    response = await _openAiService.ChatCompletion.CreateCompletion(
                        new ChatCompletionCreateRequest { Messages = messages, Model = modelId },
                        modelId,
                        cancellationToken: cancellationToken);

                    if (response.Successful)
                        return;
                }
                catch (OperationCanceledException)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "OpenAI request failed on attempt #{RetryAttempt}", retryAttempt);
                }

                context["retryAttempt"] = retryAttempt + 1;

                _log.LogInformation("Failed request #{retryAttempt} to OpenAi service: [{Code}] {Message}",
                    retryAttempt, response?.Error?.Code, response?.Error?.Message);
                throw new ChatGptException(retryAttempt);
            }, new Context(), cancellationToken);

            Guard.Against.Null(response);
            return response;
        }
    }

    public interface IGpt
    {
        Task<ChatCompletionCreateResponse> SendMessage(UpdateDecorator update, CancellationToken token);
        Task<ChatCompletionCreateResponse> SendOneOffAsync(
            string systemPrompt,
            string userPrompt,
            CancellationToken token,
            string? modelId = null);
        Task StartAnimatePhoto(
            string prompt,
            string imageFileId,
            long chatId,
            long userId,
            int progressMessageId,
            long paymentHoldId);
    }
}
