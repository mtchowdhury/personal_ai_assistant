using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using CmdNext.Models.Domain.DTOs.Ai;

namespace CmdNext.Service.Contracts
{
    /// <summary>Context a chat turn carries into tool resolution.</summary>
    public record AiToolContext(Guid? SpaceId, IReadOnlyList<AiChatMessageAttachment> PendingAttachments);

    /// <summary>
    /// A source of AI-callable tools for a chat turn. The conversation service aggregates
    /// every registered provider's tools into one list per request, so adding a new domain
    /// (finance, spaces, ...) means registering a new provider, not touching the conversation
    /// service itself.
    /// </summary>
    public interface IAiToolProvider
    {
        Task<IReadOnlyList<AITool>> GetToolsAsync(Guid userId, AiToolContext context);
    }
}
