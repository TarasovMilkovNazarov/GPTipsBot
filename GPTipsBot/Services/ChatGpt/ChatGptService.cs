using Ardalis.GuardClauses;
using GPTipsBot.Config;
using GPTipsBot.Dtos;
using GPTipsBot.Exceptions;
using GPTipsBot.Localization;
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
        private readonly ChatToolExecutor _chatToolExecutor;
        private readonly AsyncRetryPolicy _policy;

        private const int MaxRetryCount = 4;

        /// <summary>
        /// Tool-call rounds allowed per user message before we force a text-only final answer.
        /// Bounds the extra latency/cost a real agent loop adds (each round is +1 model call).
        /// </summary>
        private const int MaxToolRounds = 2;

        public ChatGptService(
            ILogger<ChatGptService> log,
            IOpenAIService openAiService,
            ContextWindow contextWindow,
            PhotoAnimationWorkflowService photoAnimationWorkflowService,
            BotSettingsRepository botSettingsRepository,
            ChatToolExecutor chatToolExecutor)
        {
            _log = log;
            _openAiService = openAiService;
            _contextWindow = contextWindow;
            _photoAnimationWorkflowService = photoAnimationWorkflowService;
            _botSettingsRepository = botSettingsRepository;
            _chatToolExecutor = chatToolExecutor;
            _policy = Policy
                .Handle<ChatGptException>()
                .WaitAndRetryAsync(MaxRetryCount, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
        }

        /// <summary>
        /// True agent loop: the model decides whether to answer directly or call a tool (e.g.
        /// image generation). When it calls one, we execute it for real, append the outcome via
        /// <see cref="ChatMessage.FromTool"/>, and ask the model again so it can weave the result
        /// into one final reply instead of us pre-deciding the intent before the model ever sees it.
        /// </summary>
        public async Task<ChatCompletionCreateResponse> SendMessage(UpdateDecorator update, CancellationToken token)
        {
            List<ChatMessage> messages;

            if (update.Message.NewContext)
            {
                messages = [new ChatMessage(update.Message.Role.ToString().ToLower(), update.Message.Text)];
            }
            else
            {
                messages = [.. _contextWindow.GetContext(update.UserChatKey, update.Message.ContextId.Value)];
            }

            var modelId = ResolveModelId(update.UserChatKey.Id);
            var toolsEnabled = _chatToolExecutor.IsEnabledFor(update);

            var response = await SendMessageInternal(
                messages.ToArray(), modelId, token, toolsEnabled ? ChatToolExecutor.Definitions : null);

            for (var round = 0; toolsEnabled && HasToolCalls(response) && round < MaxToolRounds; round++)
            {
                var assistantMessage = response!.Choices[0].Message;
                messages.Add(assistantMessage);

                foreach (var toolCall in assistantMessage.ToolCalls!)
                {
                    var result = await _chatToolExecutor.ExecuteAsync(toolCall, update, token);
                    messages.Add(ChatMessage.FromTool(result, toolCall.Id ?? string.Empty));
                }

                var allowAnotherRound = round + 1 < MaxToolRounds;
                response = await SendMessageInternal(
                    messages.ToArray(), modelId, token, allowAnotherRound ? ChatToolExecutor.Definitions : null);
            }

            return response!;
        }

        private static bool HasToolCalls(ChatCompletionCreateResponse? response) =>
            response?.Choices.FirstOrDefault()?.Message.ToolCalls is { Count: > 0 };

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

        public Task<ChatCompletionCreateResponse> SendVisionOneOffAsync(
            string systemPrompt,
            string userPrompt,
            byte[] imageBytes,
            string imageMimeSubtype,
            CancellationToken token,
            string? modelId = null)
        {
            ChatMessage[] messages =
            [
                ChatMessage.FromSystem(systemPrompt),
                ChatMessage.FromUser(
                [
                    MessageContent.TextContent(userPrompt),
                    MessageContent.ImageBinaryContent(imageBytes, imageMimeSubtype),
                ])
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
                UiLanguage = LocalizationManager.CurrentLanguage(),
            };

            return _photoAnimationWorkflowService.StartAsync(workflowData);
        }

        private string ResolveModelId(long userId) =>
            GptModelCatalog.Resolve(_botSettingsRepository.Get(userId)?.PreferredGptModel).Id;

        private async Task<ChatCompletionCreateResponse?> SendMessageInternal(
            ChatMessage[] messages,
            string modelId,
            CancellationToken cancellationToken,
            IList<ToolDefinition>? tools = null)
        {
            _log.LogInformation(
                "Send request to OpenAi service model={ModelId}: {messages}",
                modelId,
                messages.Last().Content ?? "[multimodal]");

            ChatCompletionCreateResponse? response = null;

            await _policy.ExecuteAsync(async (context, _) =>
            {
                var retryAttempt = context.TryGetValue("retryAttempt", out var value)
                    ? (int)value : 0;

                try
                {
                    response = await _openAiService.ChatCompletion.CreateCompletion(
                        new ChatCompletionCreateRequest
                        {
                            Messages = messages,
                            Model = modelId,
                            Tools = tools,
                            ToolChoice = tools != null ? ToolChoice.Auto : null,
                        },
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
        Task<ChatCompletionCreateResponse> SendVisionOneOffAsync(
            string systemPrompt,
            string userPrompt,
            byte[] imageBytes,
            string imageMimeSubtype,
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
