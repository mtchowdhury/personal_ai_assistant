using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using CmdNext.AI.Service.Generic.Configuration;
using CmdNext.AI.Service.Generic.Contracts;
using CmdNext.AI.Service.Generic.Embeddings;
using CmdNext.Repository.Contracts;
using CmdNext.Repository.Implementation;
using CmdNext.Service.Contracts;

namespace CmdNext.Service.Services
{
    public class EntryEmbeddingService : IEntryEmbeddingService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEmbeddingClientFactory _clientFactory;
        private readonly IAiCredentialResolver _credentialResolver;
        private readonly EmbeddingOptions _options;
        private readonly ILogger<EntryEmbeddingService> _logger;

        public EntryEmbeddingService(
            IUnitOfWork unitOfWork,
            IEmbeddingClientFactory clientFactory,
            IAiCredentialResolver credentialResolver,
            IOptions<EmbeddingOptions> options,
            ILogger<EntryEmbeddingService> logger)
        {
            _unitOfWork = unitOfWork;
            _clientFactory = clientFactory;
            _credentialResolver = credentialResolver;
            _options = options.Value;
            _logger = logger;
        }

        private IRepository<EntryChunk, Guid> Chunks => _unitOfWork.Repository<EntryChunk, Guid>();

        public async Task EmbedEntryAsync(Guid userId, Guid entryId, Guid spaceId, string title, string body, CancellationToken cancellationToken = default)
        {
            var chunkTexts = Chunk($"{title}\n\n{body}", _options.ChunkSize, _options.ChunkOverlap);
            if (chunkTexts.Count == 0)
            {
                await DeleteEntryChunksAsync(entryId, cancellationToken);
                return;
            }

            IReadOnlyList<Vector?> vectors;
            try
            {
                vectors = await EmbedTextsAsync(userId, chunkTexts, cancellationToken);
            }
            catch (AiNotConfiguredException)
            {
                // The user has no active credential for the embedding provider (e.g. no Mistral
                // key). This is an expected, silent no-op — not every user has one configured.
                return;
            }
            catch (Exception ex)
            {
                // Embedding is best-effort: log and leave the entry without chunks rather than
                // failing the write the user actually asked for.
                _logger.LogWarning(ex, "Failed to embed entry {EntryId}; it will not be found by semantic search until this succeeds", entryId);
                return;
            }

            var existing = await Chunks.Query().Where(c => c.EntryId == entryId).ToListAsync(cancellationToken);
            if (existing.Count > 0)
            {
                Chunks.DeleteRange(existing);
            }

            var now = DateTime.UtcNow;
            var newChunks = chunkTexts.Select((text, i) => new EntryChunk
            {
                Id = Guid.NewGuid(),
                EntryId = entryId,
                SpaceId = spaceId,
                ChunkIndex = i,
                Text = text,
                Embedding = vectors[i],
                CreatedOn = now
            }).ToList();

            await Chunks.AddRangeAsync(newChunks, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Embedded entry {EntryId} into {ChunkCount} chunk(s)", entryId, newChunks.Count);
        }

        public async Task DeleteEntryChunksAsync(Guid entryId, CancellationToken cancellationToken = default)
        {
            var existing = await Chunks.Query().Where(c => c.EntryId == entryId).ToListAsync(cancellationToken);
            if (existing.Count == 0) return;

            Chunks.DeleteRange(existing);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<SemanticMatch>> SearchAsync(
            Guid userId, string query, Guid? spaceId, IReadOnlyCollection<Guid>? entryIdFilter, int limit, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return Array.Empty<SemanticMatch>();
            }

            Vector queryVector;
            try
            {
                var vectors = await EmbedTextsAsync(userId, new[] { query }, cancellationToken);
                if (vectors[0] is not { } v) return Array.Empty<SemanticMatch>();
                queryVector = v;
            }
            catch (AiNotConfiguredException)
            {
                // No active credential for the embedding provider — silent no-op, same as EmbedEntryAsync.
                return Array.Empty<SemanticMatch>();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Semantic search embedding call failed; falling back to text search only");
                return Array.Empty<SemanticMatch>();
            }

            var q = Chunks.Query().AsNoTracking().Where(c => c.Embedding != null);
            if (spaceId is { } sid) q = q.Where(c => c.SpaceId == sid);
            if (entryIdFilter is { Count: > 0 }) q = q.Where(c => entryIdFilter.Contains(c.EntryId));

            // CosineDistance is 0 = identical, 2 = opposite; convert to a 0..1 "similarity"
            // score (1 = best) so callers/config (MinScore) read naturally.
            var results = await q
                .OrderBy(c => c.Embedding!.CosineDistance(queryVector))
                .Take(Math.Clamp(limit, 1, 100))
                .Select(c => new { c.EntryId, c.Text, Distance = c.Embedding!.CosineDistance(queryVector) })
                .ToListAsync(cancellationToken);

            return results
                .Select(r => new SemanticMatch(r.EntryId, r.Text, 1 - r.Distance / 2))
                .Where(m => m.Score >= _options.MinScore)
                .ToList();
        }

        private async Task<IReadOnlyList<Vector?>> EmbedTextsAsync(Guid userId, IReadOnlyList<string> texts, CancellationToken cancellationToken)
        {
            var provider = string.IsNullOrWhiteSpace(_options.Provider) ? "mistral" : _options.Provider!;

            // Same encrypted per-user credential chat uses (ai.UserAiProviders) — not the user's
            // default chat provider, but specifically whichever provider does embeddings, since
            // not every chat provider (e.g. Anthropic) offers an embeddings API.
            var credential = await _credentialResolver.ResolveForProviderAsync(userId, provider, _options.Model);

            var client = _clientFactory.GetClient(credential);
            var result = await client.GenerateAsync(texts, cancellationToken: cancellationToken);

            return result.Select(e => (Vector?)new Vector(e.Vector.ToArray())).ToList();
        }

        /// <summary>
        /// Splits text into overlapping character-length chunks. Simple and fast; word-boundary
        /// trimming is skipped since embedding models tolerate mid-word splits fine and this
        /// avoids pulling in a tokenizer dependency for personal-scale content.
        /// </summary>
        private static List<string> Chunk(string text, int chunkSize, int overlap)
        {
            var trimmed = text?.Trim() ?? string.Empty;
            if (trimmed.Length == 0) return new List<string>();
            if (trimmed.Length <= chunkSize) return new List<string> { trimmed };

            var step = Math.Max(1, chunkSize - overlap);
            var chunks = new List<string>();
            for (var start = 0; start < trimmed.Length; start += step)
            {
                var length = Math.Min(chunkSize, trimmed.Length - start);
                chunks.Add(trimmed.Substring(start, length));
                if (start + length >= trimmed.Length) break;
            }
            return chunks;
        }
    }
}
