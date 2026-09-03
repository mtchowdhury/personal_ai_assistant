using Microsoft.Extensions.AI;
using CmdNext.AI.Service.Generic.Configuration;

namespace CmdNext.AI.Service.Generic.Contracts
{
    public interface IProviderClientBuilder
    {
        string Provider { get; }

        IChatClient Build(AiProviderCredential credential, ProviderOptions? options);
    }
}
