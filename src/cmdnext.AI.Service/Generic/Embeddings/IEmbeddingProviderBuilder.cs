using Microsoft.Extensions.AI;
using CmdNext.AI.Service.Generic.Configuration;
using CmdNext.AI.Service.Generic.Contracts;

namespace CmdNext.AI.Service.Generic.Embeddings
{
    public interface IEmbeddingProviderBuilder
    {
        string Provider { get; }

        IEmbeddingGenerator<string, Embedding<float>> Build(AiProviderCredential credential, ProviderOptions? options);
    }
}
