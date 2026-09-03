using Microsoft.Extensions.AI;
using CmdNext.AI.Service.Generic.Contracts;

namespace CmdNext.AI.Service.Generic.Embeddings
{
    public interface IEmbeddingClientFactory
    {
        IEmbeddingGenerator<string, Embedding<float>> GetClient(AiProviderCredential credential);

        void Evict(string cacheKey);

        bool IsProviderSupported(string provider);
    }
}
