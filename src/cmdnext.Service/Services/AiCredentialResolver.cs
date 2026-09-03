using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using CmdNext.AI.Service.Generic.Contracts;
using CmdNext.Models.Domain.Model.App.Ai;
using CmdNext.Repository.Contracts;
using CmdNext.Service.Contracts;

namespace CmdNext.Service.Services
{
    public class AiCredentialResolver : IAiCredentialResolver
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICryptoHelper _cryptoHelper;
        private readonly IChatClientFactory _chatClientFactory;
        private readonly ILogger<AiCredentialResolver> _logger;

        public AiCredentialResolver(
            IUnitOfWork unitOfWork,
            ICryptoHelper cryptoHelper,
            IChatClientFactory chatClientFactory,
            ILogger<AiCredentialResolver> logger)
        {
            _unitOfWork = unitOfWork;
            _cryptoHelper = cryptoHelper;
            _chatClientFactory = chatClientFactory;
            _logger = logger;
        }

        public async Task<AiProviderCredential> ResolveAsync(Guid userId)
        {
            _logger.LogDebug("Resolving AI credential for user {UserId}", userId);

            var settings = (await _unitOfWork.Repository<UserAiSettings, Guid>()
                    .FindAsync(x => x.UserId == userId, asNoTracking: true))
                .FirstOrDefault();

            if (settings is { IsChatEnabled: false })
            {
                _logger.LogWarning("AI chat is disabled for user {UserId}", userId);
                throw new AiNotConfiguredException(
                    "AI chat is disabled for this user.");
            }

            var providers = (await _unitOfWork.Repository<UserAiProvider, Guid>()
                    .FindAsync(x => x.UserId == userId && x.IsActive, asNoTracking: true))
                .ToList();

            if (providers.Count == 0)
            {
                _logger.LogWarning("User {UserId} has no active AI provider configured", userId);
                throw new AiNotConfiguredException(
                    "No AI provider is configured for this user.");
            }

            var preferred = settings?.DefaultProvider;

            var provider = !string.IsNullOrWhiteSpace(preferred)
                ? providers.FirstOrDefault(x =>
                    string.Equals(x.Provider, preferred, StringComparison.OrdinalIgnoreCase))
                : providers[0];

            if (provider == null)
            {
                _logger.LogWarning(
                    "Preferred provider {Provider} has no active credential for user {UserId}. Available: {Available}",
                    preferred, userId, string.Join(",", providers.Select(x => x.Provider)));
                throw new AiNotConfiguredException(
                    $"The configured AI provider '{preferred}' has no active credential for this user.");
            }

            if (string.IsNullOrWhiteSpace(provider.EncryptedApiKey))
            {
                _logger.LogWarning(
                    "Provider {Provider} for user {UserId} has no stored API key", provider.Provider, userId);
                throw new AiNotConfiguredException(
                    $"The AI provider '{provider.Provider}' has no API key configured.");
            }

            if (!_chatClientFactory.IsProviderSupported(provider.Provider))
            {
                _logger.LogWarning(
                    "Provider {Provider} configured for user {UserId} is not supported by this build",
                    provider.Provider, userId);
                throw new AiNotConfiguredException(
                    $"The AI provider '{provider.Provider}' is not supported by this application.");
            }

            var model = settings?.DefaultModel;
            if (string.IsNullOrWhiteSpace(model))
            {
                _logger.LogWarning("No AI model configured for user {UserId}", userId);
                throw new AiNotConfiguredException(
                    "No AI model is configured for this user.");
            }

            string apiKey;
            try
            {
                apiKey = _cryptoHelper.Decrypt(provider.EncryptedApiKey);
            }
            catch (Exception ex)
            {
                // Usually means the crypto key changed since the key was stored.
                _logger.LogError(ex,
                    "Failed to decrypt stored API key for provider {Provider}, user {UserId}",
                    provider.Provider, userId);
                throw new AiNotConfiguredException(
                    $"The stored API key for '{provider.Provider}' could not be read. It may need to be re-entered. ({ex.Message})");
            }

            _logger.LogInformation(
                "Resolved AI credential for user {UserId}: provider {Provider}, model {Model}",
                userId, provider.Provider, model);

            return new AiProviderCredential
            {
                Provider = provider.Provider,
                ApiKey = apiKey,
                Model = model,
                Endpoint = provider.Endpoint,
                CacheKey = BuildCacheKey(userId, provider.Provider)
            };
        }

        public void Invalidate(Guid userId, string provider)
        {
            _logger.LogInformation(
                "Invalidating cached AI client for user {UserId}, provider {Provider}", userId, provider);
            _chatClientFactory.Evict(BuildCacheKey(userId, provider));
        }

        private static string BuildCacheKey(Guid userId, string provider)
        {
            return $"{userId}:{provider.ToLowerInvariant()}";
        }
    }
}
