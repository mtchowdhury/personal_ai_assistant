using System;
using Anthropic;
using Microsoft.Extensions.AI;
using CmdNext.AI.Service.Generic.Configuration;
using CmdNext.AI.Service.Generic.Contracts;

namespace CmdNext.AI.Service.Generic.Providers
{
    public sealed class AnthropicClientBuilder : IProviderClientBuilder
    {
        public string Provider => AiProviders.Anthropic;

        public IChatClient Build(AiProviderCredential credential, ProviderOptions? options)
        {
            if (string.IsNullOrWhiteSpace(credential.Model))
            {
                throw new AiNotConfiguredException(
                    $"No model configured for provider '{Provider}'.");
            }

            var client = new AnthropicClient { ApiKey = credential.ApiKey };

            return client.AsIChatClient(credential.Model);
        }
    }
}
