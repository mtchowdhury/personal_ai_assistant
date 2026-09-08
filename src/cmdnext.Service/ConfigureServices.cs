using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using CmdNext.Service.Contracts;
using CmdNext.Service.Services;
using CmdNext.Service.Tools;
using CmdNext.Service.Helpers;
using CmdNext.AI.Service.Generic.Contracts;
using Microsoft.Extensions.Hosting;

namespace CmdNext.Service
{
    public static class ConfigureServices
    {
        public static IServiceCollection ConfigureApplicationServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Configure AI Services
            services.AddScoped<IAiCredentialResolver, AiCredentialResolver>();
            services.AddScoped<IAiConversationService, AiConversationService>();

            // Configure User Service
            services.AddScoped<IUserService, UserService>();

            // Configure Finance Service
            services.AddScoped<IFinanceService, FinanceService>();
            services.AddScoped<IFinanceAiToolsFactory, FinanceAiToolsFactory>();
            services.AddScoped<IAiToolProvider, FinanceAiToolProvider>();

            // Configure Spaces Service
            services.AddScoped<ISpaceService, SpaceService>();
            services.AddSingleton<IFileStorage, LocalDiskFileStorage>();
            services.AddScoped<IAiToolProvider, SpacesAiToolProvider>();
            services.AddScoped<IEntryEmbeddingService, EntryEmbeddingService>();

            // Configure DTasks (daily tasks) Service
            services.AddScoped<IDTaskService, DTaskService>();
            services.AddScoped<IDTaskAiToolsFactory, DTaskAiToolsFactory>();
            services.AddScoped<IAiToolProvider, DTaskAiToolProvider>();

            // Configure Crypto Helper
            var cryptoSettings = configuration.GetSection("CryptoSettings");
            var cryptoKey = cryptoSettings["Key"] ?? "DefaultCryptoKeyChangeInProduction";
            services.AddSingleton<ICryptoHelper>(new AesCryptoHelper(cryptoKey));

            // Configure Attachment Helper
            services.AddSingleton<AiAttachmentHelper>();

            return services;
        }
    }
}
