using CmdNext.AI.Service.Generic.Contracts;

namespace CmdNext.AI.Service.Generic.Contracts
{
    public interface IChatClientFactory
    {
        /// <summary>
        /// Clients are cached by <see cref="AiProviderCredential.CacheKey"/>. 
        /// </summary>
        Microsoft.Extensions.AI.IChatClient GetClient(AiProviderCredential credential);

        void Evict(string cacheKey);

        bool IsProviderSupported(string provider);
    }
}
