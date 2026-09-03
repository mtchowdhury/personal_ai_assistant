using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using CmdNext.AI.Service.Generic.Chat;
using CmdNext.AI.Service.Generic.Configuration;
using CmdNext.AI.Service.Generic.Contracts;
using CmdNext.AI.Service.Generic.Embeddings;
using CmdNext.AI.Service.Generic.Providers;

namespace CmdNext.AI.Service.Generic
{
    public static class ConfigureGenericAiServices
    {
        public static IServiceCollection AddGenericAiServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.Configure<AiOptions>(configuration.GetSection(AiOptions.SectionName));
            services.Configure<EmbeddingOptions>(configuration.GetSection(EmbeddingOptions.SectionName));

            services.AddMemoryCache();

            services.AddSingleton<IProviderClientBuilder, AnthropicClientBuilder>();
            services.AddSingleton<IProviderClientBuilder, MistralClientBuilder>();
            services.AddSingleton<IChatClientFactory, ChatClientFactory>();

            services.AddScoped<IAiChatService, AiChatService>();

            services.AddSingleton<IEmbeddingProviderBuilder, MistralEmbeddingClientBuilder>();
            services.AddSingleton<IEmbeddingClientFactory, EmbeddingClientFactory>();

            return services;
        }
    }
}
