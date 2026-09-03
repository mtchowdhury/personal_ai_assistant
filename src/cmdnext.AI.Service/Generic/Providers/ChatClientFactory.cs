using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using CmdNext.AI.Service.Generic.Configuration;
using CmdNext.AI.Service.Generic.Contracts;

namespace CmdNext.AI.Service.Generic.Providers
{
    public sealed class ChatClientFactory : IChatClientFactory
    {
        private const string CacheKeyPrefix = "ai:client:";

        private readonly IReadOnlyDictionary<string, IProviderClientBuilder> _builders;
        private readonly IMemoryCache _cache;
        private readonly AiOptions _options;

        public ChatClientFactory(
            IEnumerable<IProviderClientBuilder> builders,
            IMemoryCache cache,
            IOptions<AiOptions> options)
        {
            _builders = builders.ToDictionary(
                b => b.Provider,
                StringComparer.OrdinalIgnoreCase);
            _cache = cache;
            _options = options.Value;
        }

        public IChatClient GetClient(AiProviderCredential credential)
        {
            var cacheKey = CacheKeyPrefix + credential.CacheKey;

            if (_cache.TryGetValue(cacheKey, out IChatClient? cached) && cached is not null)
            {
                return cached;
            }

            if (!_builders.TryGetValue(credential.Provider, out var builder))
            {
                throw new NotSupportedException(
                    $"AI provider '{credential.Provider}' is not supported.");
            }

            _options.Providers.TryGetValue(credential.Provider, out var providerOptions);

            if (providerOptions is { IsEnabled: false })
            {
                throw new InvalidOperationException(
                    $"AI provider '{credential.Provider}' is disabled.");
            }

            var client = builder.Build(credential, providerOptions);
            client = client.AsBuilder().UseFunctionInvocation().Build();

            var entryOptions = new MemoryCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromMinutes(_options.ClientCacheMinutes)
            };
            entryOptions.RegisterPostEvictionCallback(static (_, value, _, _) =>
            {
                (value as IDisposable)?.Dispose();
            });

            _cache.Set(cacheKey, client, entryOptions);

            return client;
        }

        public void Evict(string cacheKey)
        {
            _cache.Remove(CacheKeyPrefix + cacheKey);
        }

        public bool IsProviderSupported(string provider)
        {
            return !string.IsNullOrWhiteSpace(provider) && _builders.ContainsKey(provider);
        }
    }
}
