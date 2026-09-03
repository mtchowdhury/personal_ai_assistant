using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CmdNext.Service.Contracts
{
    public record SemanticMatch(Guid EntryId, string ChunkText, double Score);

    /// <summary>
    /// Chunks and embeds entry bodies for semantic search, and runs the similarity query.
    /// Uses the same per-user encrypted provider credential as chat (via IAiCredentialResolver,
    /// resolving the provider named in AI:Embedding:Provider, e.g. "mistral") — no separate
    /// embedding key to configure. Embedding is best-effort: when a user has no active
    /// credential for that provider, writes still succeed (chunks just have no vector) and
    /// semantic search silently returns no matches so callers fall back to text search — a
    /// missing credential must never break the app, same policy as finance budgets.
    /// </summary>
    public interface IEntryEmbeddingService
    {
        /// <summary>Re-chunks and re-embeds one entry's current body, replacing its old chunks.</summary>
        Task EmbedEntryAsync(Guid userId, Guid entryId, Guid spaceId, string title, string body, CancellationToken cancellationToken = default);

        Task DeleteEntryChunksAsync(Guid entryId, CancellationToken cancellationToken = default);

        /// <summary>Finds the best-matching chunks for a query, optionally scoped to a space and/or a set of entry ids.</summary>
        Task<IReadOnlyList<SemanticMatch>> SearchAsync(
            Guid userId, string query, Guid? spaceId, IReadOnlyCollection<Guid>? entryIdFilter, int limit, CancellationToken cancellationToken = default);
    }
}
