using System;
using Microsoft.Extensions.AI;
using System.ClientModel;
using CmdNext.AI.Service.Generic.Configuration;
using CmdNext.AI.Service.Generic.Contracts;
using OpenAI;

namespace CmdNext.AI.Service.Generic.Embeddings
{
    public sealed class MistralEmbeddingClientBuilder : IEmbeddingProviderBuilder
    {
        private const string DefaultEndpoint = "https://api.mistral.ai/v1";

        public string Provider => AiProviders.Mistral;

        public IEmbeddingGenerator<string, Embedding<float>> Build(AiProviderCredential credential, ProviderOptions? options)
        {
            if (string.IsNullOrWhiteSpace(credential.Model))
            {
                throw new AiNotConfiguredException(
                    $"No embedding model configured for provider '{Provider}'.");
            }

            var endpoint = credential.Endpoint
                ?? options?.Endpoint
                ?? DefaultEndpoint;

            var client = new OpenAIClient(
                new ApiKeyCredential(credential.ApiKey),
                new OpenAIClientOptions { Endpoint = new Uri(endpoint) });

            return client.GetEmbeddingClient(credential.Model).AsIEmbeddingGenerator();
        }
    }
}
