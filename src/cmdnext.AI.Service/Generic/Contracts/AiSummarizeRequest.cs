using System.Collections.Generic;
using Microsoft.Extensions.AI;

namespace CmdNext.AI.Service.Generic.Contracts
{
    public sealed class AiSummarizeRequest
    {
        public required AiProviderCredential Credential { get; init; }

        public required IReadOnlyList<ChatMessage> Messages { get; init; }

        public string? ExistingSummary { get; init; }

        public int? MaxOutputTokens { get; init; }
    }
}
