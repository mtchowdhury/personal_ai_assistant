using System.Threading.Tasks;
using CmdNext.AI.Service.Generic.Contracts;

namespace CmdNext.Service.Contracts
{
    public interface IAiCredentialResolver
    {
        /// <summary>
        /// Resolve AI credentials for a user. Throws <see cref="AiNotConfiguredException"
        /// when nothing usable is configured.
        /// </summary>
        Task<AiProviderCredential> ResolveAsync(Guid userId);

        /// <summary>
        /// Resolve a specific provider's credential for a user (e.g. "mistral" for embeddings),
        /// regardless of their default chat provider/model. Throws <see cref="AiNotConfiguredException"/>
        /// when the user has no active, decryptable credential for that provider.
        /// </summary>
        Task<AiProviderCredential> ResolveForProviderAsync(Guid userId, string provider, string? model);

        void Invalidate(Guid userId, string provider);
    }
}
