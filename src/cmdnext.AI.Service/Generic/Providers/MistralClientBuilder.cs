using System;
using Microsoft.Extensions.AI;
using System.ClientModel;
using CmdNext.AI.Service.Generic.Configuration;
using CmdNext.AI.Service.Generic.Contracts;
using OpenAI;

namespace CmdNext.AI.Service.Generic.Providers
{
    /// <summary>
    /// Mistral over its OpenAI-compatible endpoint. Chat, streaming, and tool calling
    /// work; Mistral's own Agents API and its MCP integration are not reachable this
    /// way and stay with the legacy mapper pipeline.
    /// </summary>
    public sealed class MistralClientBuilder : IProviderClientBuilder
    {
        private const string DefaultEndpoint = "https://api.mistral.ai/v1";

        public string Provider => AiProviders.Mistral;

        public IChatClient Build(AiProviderCredential credential, ProviderOptions? options)
        {
            if (string.IsNullOrWhiteSpace(credential.Model))
            {
                throw new AiNotConfiguredException(
                    $"No model configured for provider '{Provider}'.");
            }

            var endpoint = credential.Endpoint
                ?? options?.Endpoint
                ?? DefaultEndpoint;

            var client = new OpenAIClient(
                new ApiKeyCredential(credential.ApiKey),
                new OpenAIClientOptions { Endpoint = new Uri(endpoint) });

            return client.GetChatClient(credential.Model).AsIChatClient();
        }
    }
}