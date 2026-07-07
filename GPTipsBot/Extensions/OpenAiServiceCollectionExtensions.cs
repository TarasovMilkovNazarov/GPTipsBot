using GPTipsBot.Config;
using GPTipsBot.Services;
using Microsoft.Extensions.DependencyInjection;
using OpenAI;
using OpenAI.Interfaces;
using OpenAI.Managers;
using GptModels = OpenAI.ObjectModels;

namespace GPTipsBot.Extensions;

public static class OpenAiServiceCollectionExtensions
{
    public static IServiceCollection AddOpenAiClient(this IServiceCollection services)
    {
        services.AddSingleton<HappHttpClient>();
        services.AddSingleton<IOpenAIService>(sp => new OpenAIService(
            new OpenAiOptions
            {
                ApiKey = AppConfig.OpenAiToken,
                DefaultModelId = GptModels.Models.Gpt_3_5_Turbo
            },
            sp.GetRequiredService<HappHttpClient>()));

        return services;
    }
}
