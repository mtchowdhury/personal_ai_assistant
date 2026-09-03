using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CmdNext.AI.Service.Generic.Contracts;
using CmdNext.Models.Domain.DTOs.Ai;

namespace CmdNext.Service.Contracts
{
    public interface IAiConversationService
    {
        Task<AiChatSessionDto> CreateSessionAsync(Guid userId, string? title = null, string? profileName = null, Guid? spaceId = null);

        Task<List<AiChatSessionDto>> GetSessionsAsync(Guid userId);

        Task<AiChatSessionDetailDto> GetSessionAsync(Guid sessionId, Guid userId);

        Task RenameSessionAsync(Guid sessionId, Guid userId, string title);

        Task DeleteSessionAsync(Guid sessionId, Guid userId);

        /// <summary>
        /// Persists the user message, streams the assistant reply, then persists it
        /// along with a usage record. Compaction runs first when the session has grown
        /// past the configured threshold.
        /// </summary>
        IAsyncEnumerable<CmdNext.AI.Service.Generic.Contracts.AiChatStreamUpdate> SendMessageAsync(
            Guid sessionId,
            Guid userId,
            string message,
            List<AiChatAttachmentDto>? attachments = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Compacts the session on demand, independent of the automatic threshold.
        /// </summary>
        Task<bool> CompactSessionAsync(Guid sessionId, Guid userId, CancellationToken cancellationToken = default);
    }
}
