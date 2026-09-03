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

        void Invalidate(Guid userId, string provider);
    }
}
