using System.Collections.Generic;
using Microsoft.Extensions.AI;

namespace CmdNext.AI.Service.Generic.Contracts
{
    public sealed class AiChatRequest
    {
        public required AiProviderCredential Credential { get; init; }

        /// <summary>
        /// Full conversation to send, oldest first. The caller is responsible for
        /// loading history, applying any windowing, and appending the new user message.
        /// </summary>
        public required IReadOnlyList<ChatMessage> Messages { get; init; }

        public string? SystemPrompt { get; init; }

        /// <summary>
        /// Summary of the compacted portion of the conversation, injected after the
        /// system prompt and before <see cref="Messages"/>. 
        /// </summary>
        public string? ConversationSummary { get; init; }

        /// <summary>
        /// Tools built by the Service layer (they need UoW / tenancy), passed through
        /// to the model. This layer defines none of its own.
        /// </summary>
        public IReadOnlyList<AITool>? Tools { get; init; }

        public float? Temperature { get; init; }

        public int? MaxOutputTokens { get; init; }

        public string? ProfileName { get; init; }
    }
}
