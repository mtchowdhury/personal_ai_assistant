using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CmdNext.Models.Domain.DTOs.Spaces;
using CmdNext.Models.Domain.Model.App.Spaces;
using CmdNext.Repository.Contracts;
using CmdNext.Service.Contracts;
using CmdNext.Service.Spaces;

namespace CmdNext.Service.Services
{
    public class SpaceService : ISpaceService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFileStorage _fileStorage;
        private readonly IEntryEmbeddingService _embeddings;
        private readonly ILogger<SpaceService> _logger;

        public SpaceService(
            IUnitOfWork unitOfWork, IFileStorage fileStorage, IEntryEmbeddingService embeddings, ILogger<SpaceService> logger)
        {
            _unitOfWork = unitOfWork;
            _fileStorage = fileStorage;
            _embeddings = embeddings;
            _logger = logger;
        }

        private IRepository<Space, Guid> Spaces => _unitOfWork.Repository<Space, Guid>();
        private IRepository<Node, Guid> Nodes => _unitOfWork.Repository<Node, Guid>();
        private IRepository<Entry, Guid> Entries => _unitOfWork.Repository<Entry, Guid>();
        private IRepository<Attachment, Guid> Attachments => _unitOfWork.Repository<Attachment, Guid>();

        // ---- Templates ----

        public List<SpaceTemplateDto> GetTemplates() => SpaceTemplates.All;

        // ---- Space ----

        public async Task<List<SpaceDto>> GetSpacesAsync(Guid userId, bool includeArchived = false)
        {
            var query = Spaces.Query().AsNoTracking().Where(s => s.UserId == userId);
            if (!includeArchived) query = query.Where(s => s.Status != "archived");

            return await query.OrderBy(s => s.Name).Select(s => ToSpaceDto(s)).ToListAsync();
        }

        public async Task<SpaceDto> GetSpaceAsync(Guid userId, Guid spaceId)
        {
            var space = await FindSpaceAsync(userId, spaceId);
            return ToSpaceDto(space);
        }

        public async Task<SpaceDto> CreateSpaceAsync(Guid userId, CreateSpaceRequest request)
        {
            var name = request.Name?.Trim();
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Space name is required.");

            var template = SpaceTemplates.Resolve(request.Kind);
            var slug = await GenerateUniqueSlugAsync(userId, name);

            var space = new Space
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Name = name,
                Slug = slug,
                Kind = template.Kind,
                Description = request.Description?.Trim(),
                Conventions = request.Conventions?.Trim(),
                SettingsJson = "{}",
                StateJson = "{}",
                SchemaJson = JsonSerializer.Serialize(template.EntryTypes),
                Status = "active",
                CreatedOn = DateTime.UtcNow,
                CreatedBy = userId
            };

            await Spaces.AddAsync(space);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Created space {SpaceId} ({Kind}) for user {UserId}", space.Id, space.Kind, userId);
            return ToSpaceDto(space);
        }

        public async Task<SpaceDto> UpdateSpaceAsync(Guid userId, Guid spaceId, UpdateSpaceRequest request)
        {
            var space = await FindSpaceAsync(userId, spaceId);

            if (request.Name != null)
            {
                var name = request.Name.Trim();
                if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Space name is required.");
                space.Name = name;
            }
            if (request.Description != null) space.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
            if (request.Conventions != null) space.Conventions = string.IsNullOrWhiteSpace(request.Conventions) ? null : request.Conventions.Trim();
            if (request.SettingsJson != null) space.SettingsJson = ValidateJson(request.SettingsJson, "settings");
            if (request.Status != null)
            {
                if (request.Status != "active" && request.Status != "archived")
                    throw new ArgumentException("Status must be 'active' or 'archived'.");
                space.Status = request.Status;
            }

            space.UpdatedOn = DateTime.UtcNow;
            space.UpdatedBy = userId;

            Spaces.Update(space);
            await _unitOfWork.SaveChangesAsync();
            return ToSpaceDto(space);
        }

        public async Task<SpaceDto> UpdateSpaceStateAsync(Guid userId, Guid spaceId, UpdateSpaceStateRequest request)
        {
            var space = await FindSpaceAsync(userId, spaceId);
            space.StateJson = MergeJson(space.StateJson, request.StatePatchJson);
            space.UpdatedOn = DateTime.UtcNow;
            space.UpdatedBy = userId;

            Spaces.Update(space);
            await _unitOfWork.SaveChangesAsync();
            return ToSpaceDto(space);
        }

        public async Task<SpaceDto> UpdateSpaceSchemaAsync(Guid userId, Guid spaceId, UpdateSpaceSchemaRequest request)
        {
            var space = await FindSpaceAsync(userId, spaceId);
            space.SchemaJson = ValidateJson(request.SchemaJson, "schema");
            space.UpdatedOn = DateTime.UtcNow;
            space.UpdatedBy = userId;

            Spaces.Update(space);
            await _unitOfWork.SaveChangesAsync();
            return ToSpaceDto(space);
        }

        public async Task ArchiveSpaceAsync(Guid userId, Guid spaceId)
        {
            var space = await FindSpaceAsync(userId, spaceId);
            space.Status = "archived";
            space.UpdatedOn = DateTime.UtcNow;
            space.UpdatedBy = userId;

            Spaces.Update(space);
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task DeleteSpaceAsync(Guid userId, Guid spaceId)
        {
            var space = await FindSpaceAsync(userId, spaceId);

            // Attachments' files must be removed explicitly; DB rows cascade.
            var attachments = await Attachments.Query().Where(a => a.SpaceId == spaceId).ToListAsync();
            foreach (var a in attachments)
            {
                await _fileStorage.DeleteAsync(a.StoragePath);
            }

            Spaces.Delete(space);
            await _unitOfWork.SaveChangesAsync();
        }

        // ---- Node ----

        public async Task<List<NodeDto>> GetNodesAsync(Guid userId, Guid spaceId)
        {
            await FindSpaceAsync(userId, spaceId);

            var nodes = await Nodes.Query().AsNoTracking().Where(n => n.SpaceId == spaceId)
                .OrderBy(n => n.Path).ToListAsync();

            var entryCounts = await Entries.Query().AsNoTracking()
                .Where(e => e.SpaceId == spaceId && e.NodeId != null && e.DeletedOn == null)
                .GroupBy(e => e.NodeId!.Value)
                .Select(g => new { NodeId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.NodeId, x => x.Count);

            var childCounts = nodes.Where(n => n.ParentId != null)
                .GroupBy(n => n.ParentId!.Value)
                .ToDictionary(g => g.Key, g => g.Count());

            return nodes.Select(n => ToNodeDto(n, entryCounts, childCounts)).ToList();
        }

        public async Task<NodeDto> CreateNodeAsync(Guid userId, Guid spaceId, CreateNodeRequest request)
        {
            await FindSpaceAsync(userId, spaceId);

            var name = request.Name?.Trim();
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Node name is required.");

            string parentPath = "";
            if (request.ParentId is { } parentId)
            {
                var parent = await Nodes.Query().FirstOrDefaultAsync(n => n.Id == parentId && n.SpaceId == spaceId)
                    ?? throw new KeyNotFoundException("Parent node not found.");
                parentPath = parent.Path;
            }

            var slug = Slugify(name);
            var path = string.IsNullOrEmpty(parentPath) ? slug : $"{parentPath}/{slug}";
            path = await MakeUniquePathAsync(spaceId, path);

            var maxSort = await Nodes.Query()
                .Where(n => n.SpaceId == spaceId && n.ParentId == request.ParentId)
                .Select(n => (int?)n.SortOrder).MaxAsync() ?? -1;

            var node = new Node
            {
                Id = Guid.NewGuid(),
                SpaceId = spaceId,
                ParentId = request.ParentId,
                Name = name,
                Path = path,
                SortOrder = maxSort + 1,
                Kind = request.Kind?.Trim(),
                Summary = request.Summary?.Trim(),
                CreatedOn = DateTime.UtcNow,
                CreatedBy = userId
            };

            await Nodes.AddAsync(node);
            await _unitOfWork.SaveChangesAsync();
            return ToNodeDto(node, new Dictionary<Guid, int>(), new Dictionary<Guid, int>());
        }

        public async Task<NodeDto> UpdateNodeAsync(Guid userId, Guid spaceId, Guid nodeId, UpdateNodeRequest request)
        {
            await FindSpaceAsync(userId, spaceId);
            var node = await Nodes.Query().FirstOrDefaultAsync(n => n.Id == nodeId && n.SpaceId == spaceId)
                ?? throw new KeyNotFoundException("Node not found.");

            var nameChanged = false;
            if (request.Name != null)
            {
                var name = request.Name.Trim();
                if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Node name is required.");
                node.Name = name;
                nameChanged = true;
            }

            var parentChanged = false;
            if (request.ParentIdSet)
            {
                if (request.ParentId == nodeId) throw new ArgumentException("A node cannot be its own parent.");
                if (request.ParentId is { } newParentId)
                {
                    var newParent = await Nodes.Query().FirstOrDefaultAsync(n => n.Id == newParentId && n.SpaceId == spaceId)
                        ?? throw new KeyNotFoundException("Parent node not found.");
                    if (newParent.Path == node.Path || newParent.Path.StartsWith(node.Path + "/"))
                        throw new ArgumentException("Cannot move a node under its own descendant.");
                }
                node.ParentId = request.ParentId;
                parentChanged = true;
            }

            if (request.Summary != null) node.Summary = string.IsNullOrWhiteSpace(request.Summary) ? null : request.Summary.Trim();
            if (request.SortOrder is { } sort) node.SortOrder = sort;

            if (nameChanged || parentChanged)
            {
                var oldPath = node.Path;
                string parentPath = "";
                if (node.ParentId is { } pid)
                {
                    var parent = await Nodes.Query().AsNoTracking().FirstAsync(n => n.Id == pid);
                    parentPath = parent.Path;
                }
                var slug = Slugify(node.Name);
                var newPath = string.IsNullOrEmpty(parentPath) ? slug : $"{parentPath}/{slug}";
                if (newPath != oldPath)
                {
                    newPath = await MakeUniquePathAsync(spaceId, newPath, excludeNodeId: nodeId);
                    await RepathDescendantsAsync(spaceId, oldPath, newPath);
                    node.Path = newPath;
                }
            }

            node.UpdatedOn = DateTime.UtcNow;
            node.UpdatedBy = userId;

            Nodes.Update(node);
            await _unitOfWork.SaveChangesAsync();
            return ToNodeDto(node, new Dictionary<Guid, int>(), new Dictionary<Guid, int>());
        }

        public async Task DeleteNodeAsync(Guid userId, Guid spaceId, Guid nodeId)
        {
            await FindSpaceAsync(userId, spaceId);
            var node = await Nodes.Query().FirstOrDefaultAsync(n => n.Id == nodeId && n.SpaceId == spaceId)
                ?? throw new KeyNotFoundException("Node not found.");

            var hasChildren = await Nodes.Query().AnyAsync(n => n.ParentId == nodeId);
            if (hasChildren) throw new InvalidOperationException("Delete or move child nodes first.");

            var hasEntries = await Entries.Query().AnyAsync(e => e.NodeId == nodeId && e.DeletedOn == null);
            if (hasEntries) throw new InvalidOperationException("This node still has entries. Move or delete them first.");

            var attachments = await Attachments.Query().Where(a => a.NodeId == nodeId).ToListAsync();
            foreach (var a in attachments) await _fileStorage.DeleteAsync(a.StoragePath);

            Nodes.Delete(node);
            await _unitOfWork.SaveChangesAsync();
        }

        // ---- Entry ----

        public async Task<List<EntryListItemDto>> GetEntriesAsync(Guid userId, Guid spaceId, EntryQuery query)
        {
            await FindSpaceAsync(userId, spaceId);

            var q = Entries.Query().AsNoTracking()
                .Where(e => e.SpaceId == spaceId && e.DeletedOn == null);

            q = await ApplyNodeFilterAsync(q, spaceId, query.NodeId, query.IncludeDescendants);

            if (!string.IsNullOrWhiteSpace(query.Type)) q = q.Where(e => e.Type == query.Type);
            if (!string.IsNullOrWhiteSpace(query.Status)) q = q.Where(e => e.Status == query.Status);
            if (query.Tags is { Count: > 0 })
            {
                foreach (var tag in query.Tags) q = q.Where(e => e.Tags.Contains(tag));
            }
            if (query.From is { } from) q = q.Where(e => e.OccurredOn != null && e.OccurredOn >= from);
            if (query.To is { } to) q = q.Where(e => e.OccurredOn != null && e.OccurredOn < to);
            if (query.Fields is { Count: > 0 })
            {
                foreach (var kv in query.Fields)
                {
                    var key = kv.Key;
                    var value = kv.Value;
                    q = q.Where(e => EF.Functions.JsonExists(e.FieldsJson, key) &&
                                      EF.Property<string>(e, "FieldsJson") != null &&
                                      e.FieldsJson.Contains(value));
                }
            }

            var take = Math.Clamp(query.Take, 1, 500);
            var rows = await q.OrderByDescending(e => e.OccurredOn ?? e.CreatedOn).Take(take).ToListAsync();

            var nodePaths = await GetNodePathsAsync(spaceId, rows.Select(r => r.NodeId));
            return rows.Select(e => ToListItemDto(e, nodePaths)).ToList();
        }

        public async Task<EntryDto> GetEntryAsync(Guid userId, Guid spaceId, Guid entryId)
        {
            await FindSpaceAsync(userId, spaceId);
            var entry = await Entries.Query().AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == entryId && e.SpaceId == spaceId && e.DeletedOn == null)
                ?? throw new KeyNotFoundException("Entry not found.");

            var attachmentCount = await Attachments.Query().CountAsync(a => a.EntryId == entryId);
            var nodePaths = await GetNodePathsAsync(spaceId, new[] { entry.NodeId });
            return ToEntryDto(entry, nodePaths, attachmentCount);
        }

        public async Task<EntryDto> CreateEntryAsync(Guid userId, Guid spaceId, CreateEntryRequest request)
        {
            var space = await FindSpaceAsync(userId, spaceId);

            var title = request.Title?.Trim();
            if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Entry title is required.");

            var type = ResolveEntryType(space, request.Type);

            if (request.NodeId is { } nodeId)
            {
                var exists = await Nodes.Query().AnyAsync(n => n.Id == nodeId && n.SpaceId == spaceId);
                if (!exists) throw new KeyNotFoundException("Node not found.");
            }

            var entry = new Entry
            {
                Id = Guid.NewGuid(),
                SpaceId = spaceId,
                NodeId = request.NodeId,
                UserId = userId,
                Type = type,
                Title = title,
                Body = request.Body ?? string.Empty,
                FieldsJson = ValidateJson(request.FieldsJson ?? "{}", "fields"),
                Tags = NormalizeTags(request.Tags),
                OccurredOn = request.OccurredOn,
                DueOn = request.DueOn,
                Status = request.Status?.Trim(),
                Source = string.Equals(request.Source, "ai", StringComparison.OrdinalIgnoreCase) ? "ai" : "manual",
                CreatedOn = DateTime.UtcNow,
                CreatedBy = userId
            };

            await Entries.AddAsync(entry);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Created {Source} entry {EntryId} ({Type}) in space {SpaceId}", entry.Source, entry.Id, entry.Type, spaceId);

            await _embeddings.EmbedEntryAsync(entry.Id, spaceId, entry.Title, entry.Body);

            var nodePaths = await GetNodePathsAsync(spaceId, new[] { entry.NodeId });
            return ToEntryDto(entry, nodePaths, 0);
        }

        public async Task<EntryDto> UpdateEntryAsync(Guid userId, Guid spaceId, Guid entryId, UpdateEntryRequest request)
        {
            await FindSpaceAsync(userId, spaceId);
            var entry = await Entries.Query().FirstOrDefaultAsync(e => e.Id == entryId && e.SpaceId == spaceId && e.DeletedOn == null)
                ?? throw new KeyNotFoundException("Entry not found.");

            if (request.Title != null)
            {
                var title = request.Title.Trim();
                if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Entry title is required.");
                entry.Title = title;
            }
            if (request.Body != null) entry.Body = request.Body;
            if (request.FieldsJson != null) entry.FieldsJson = ValidateJson(request.FieldsJson, "fields");
            if (request.Tags != null) entry.Tags = NormalizeTags(request.Tags);
            if (request.OccurredOn.HasValue) entry.OccurredOn = request.OccurredOn;
            if (request.DueOn.HasValue) entry.DueOn = request.DueOn;
            if (request.Status != null) entry.Status = string.IsNullOrWhiteSpace(request.Status) ? null : request.Status.Trim();
            if (request.NodeIdSet)
            {
                if (request.NodeId is { } nid)
                {
                    var exists = await Nodes.Query().AnyAsync(n => n.Id == nid && n.SpaceId == spaceId);
                    if (!exists) throw new KeyNotFoundException("Node not found.");
                }
                entry.NodeId = request.NodeId;
            }

            entry.UpdatedOn = DateTime.UtcNow;
            entry.UpdatedBy = userId;

            Entries.Update(entry);
            await _unitOfWork.SaveChangesAsync();

            if (request.Title != null || request.Body != null)
            {
                await _embeddings.EmbedEntryAsync(entry.Id, spaceId, entry.Title, entry.Body);
            }

            var nodePaths = await GetNodePathsAsync(spaceId, new[] { entry.NodeId });
            var attachmentCount = await Attachments.Query().CountAsync(a => a.EntryId == entryId);
            return ToEntryDto(entry, nodePaths, attachmentCount);
        }

        public async Task<EntryDto> AppendToEntryAsync(Guid userId, Guid spaceId, Guid entryId, AppendToEntryRequest request)
        {
            await FindSpaceAsync(userId, spaceId);
            var entry = await Entries.Query().FirstOrDefaultAsync(e => e.Id == entryId && e.SpaceId == spaceId && e.DeletedOn == null)
                ?? throw new KeyNotFoundException("Entry not found.");

            var text = request.Text?.Trim();
            if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Text to append is required.");

            var heading = string.IsNullOrWhiteSpace(request.Heading)
                ? $"Update — {DateTime.UtcNow:yyyy-MM-dd}"
                : request.Heading!.Trim();

            entry.Body = $"{entry.Body.TrimEnd()}\n\n## {heading}\n\n{text}\n";
            entry.UpdatedOn = DateTime.UtcNow;
            entry.UpdatedBy = userId;

            Entries.Update(entry);
            await _unitOfWork.SaveChangesAsync();

            await _embeddings.EmbedEntryAsync(entry.Id, spaceId, entry.Title, entry.Body);

            var nodePaths = await GetNodePathsAsync(spaceId, new[] { entry.NodeId });
            var attachmentCount = await Attachments.Query().CountAsync(a => a.EntryId == entryId);
            return ToEntryDto(entry, nodePaths, attachmentCount);
        }

        public async Task DeleteEntryAsync(Guid userId, Guid spaceId, Guid entryId)
        {
            await FindSpaceAsync(userId, spaceId);
            var entry = await Entries.Query().FirstOrDefaultAsync(e => e.Id == entryId && e.SpaceId == spaceId && e.DeletedOn == null)
                ?? throw new KeyNotFoundException("Entry not found.");

            entry.DeletedOn = DateTime.UtcNow;
            entry.UpdatedOn = DateTime.UtcNow;
            entry.UpdatedBy = userId;

            Entries.Update(entry);
            await _unitOfWork.SaveChangesAsync();

            await _embeddings.DeleteEntryChunksAsync(entryId);
        }

        // ---- Search ----

        public async Task<List<SearchResultDto>> SearchAsync(Guid userId, SearchEntriesRequest request)
        {
            var limit = Math.Clamp(request.Limit, 1, 100);

            var q = Entries.Query().AsNoTracking()
                .Include(e => e.Space)
                .Where(e => e.UserId == userId && e.DeletedOn == null);

            if (request.SpaceId is { } spaceId)
            {
                await FindSpaceAsync(userId, spaceId);
                q = q.Where(e => e.SpaceId == spaceId);
                q = await ApplyNodeFilterAsync(q, spaceId, request.NodeId, request.IncludeDescendants);
            }

            if (!string.IsNullOrWhiteSpace(request.Type)) q = q.Where(e => e.Type == request.Type);
            if (request.Tags is { Count: > 0 })
            {
                foreach (var tag in request.Tags) q = q.Where(e => e.Tags.Contains(tag));
            }
            if (request.From is { } from) q = q.Where(e => e.OccurredOn != null && e.OccurredOn >= from);
            if (request.To is { } to) q = q.Where(e => e.OccurredOn != null && e.OccurredOn < to);

            var term = request.Query?.Trim();
            if (string.IsNullOrWhiteSpace(term))
            {
                var recent = await q.OrderByDescending(e => e.OccurredOn ?? e.CreatedOn).Take(limit).ToListAsync();
                var paths = await GetNodePathsAsync(recent.Select(e => (e.SpaceId, e.NodeId)));
                return recent.Select(e => ToSearchResult(e, paths, 0, e.Body.Length > 240 ? e.Body[..240] + "…" : e.Body)).ToList();
            }

            var mode = string.IsNullOrWhiteSpace(request.Mode) ? "hybrid" : request.Mode.ToLowerInvariant();

            List<(Entry Entry, double Rank, string Snippet)> textHits = new();
            if (mode is "text" or "hybrid")
            {
                textHits = await TextSearchAsync(q, term, limit);
            }

            List<(Entry Entry, double Rank, string Snippet)> semanticHits = new();
            if (mode is "semantic" or "hybrid")
            {
                // Semantic search runs over the chunk table directly, then the candidate entry
                // ids are re-applied against the same filtered `q` so space/node/type/tag/date
                // scoping stays identical between modes.
                var candidateIds = await q.Select(e => e.Id).ToListAsync();
                semanticHits = await SemanticSearchAsync(candidateIds, request.SpaceId, term, limit);
            }

            // Rank: text hits first (they're exact/near-exact matches), then semantic-only hits
            // for entries text search missed, in similarity order. Same entry from both keeps
            // its text rank and isn't duplicated.
            var merged = new List<(Entry Entry, double Rank, string Snippet)>(textHits);
            var seen = textHits.Select(h => h.Entry.Id).ToHashSet();
            foreach (var hit in semanticHits)
            {
                if (seen.Add(hit.Entry.Id)) merged.Add(hit);
            }

            var results = merged.Take(limit).ToList();
            var nodePaths2 = await GetNodePathsAsync(results.Select(r => (r.Entry.SpaceId, r.Entry.NodeId)));
            return results.Select(r => ToSearchResult(r.Entry, nodePaths2, r.Rank, r.Snippet)).ToList();
        }

        /// <summary>
        /// Full-text search over the already-filtered query, with a trigram fallback for
        /// typos/partial words when FTS finds nothing.
        /// </summary>
        private async Task<List<(Entry, double, string)>> TextSearchAsync(IQueryable<Entry> q, string term, int limit)
        {
            // A generated `tsvector` expression over Title + Body is covered by a GIN index
            // (see migration); ts_rank orders by relevance. websearch_to_tsquery handles quoted
            // phrases and plain multi-word input.
            var ftsQuery = q
                .Where(e => EF.Functions.ToTsVector("simple", e.Title + " " + e.Body)
                    .Matches(EF.Functions.WebSearchToTsQuery("simple", term)))
                .Select(e => new
                {
                    Entry = e,
                    Rank = EF.Functions.ToTsVector("simple", e.Title + " " + e.Body)
                        .RankCoverDensity(EF.Functions.WebSearchToTsQuery("simple", term))
                })
                .OrderByDescending(x => x.Rank)
                .Take(limit)
                .ToList();

            if (ftsQuery.Count > 0)
            {
                return ftsQuery.Select(x => (x.Entry, (double)x.Rank, BuildSnippet(x.Entry.Body, term))).ToList();
            }

            var trigramHits = await q
                .Where(e => EF.Functions.TrigramsSimilarity(e.Title, term) > 0.2
                         || EF.Functions.TrigramsSimilarity(e.Body, term) > 0.15)
                .OrderByDescending(e => EF.Functions.TrigramsSimilarity(e.Title, term))
                .Take(limit)
                .ToListAsync();

            return trigramHits.Select(e => (e, 1.0, BuildSnippet(e.Body, term))).ToList();
        }

        private async Task<List<(Entry, double, string)>> SemanticSearchAsync(
            List<Guid> candidateEntryIds, Guid? spaceId, string term, int limit)
        {
            if (candidateEntryIds.Count == 0) return new List<(Entry, double, string)>();

            var matches = await _embeddings.SearchAsync(term, spaceId, candidateEntryIds, limit);
            if (matches.Count == 0) return new List<(Entry, double, string)>();

            var entryIds = matches.Select(m => m.EntryId).Distinct().ToList();
            var entries = await Entries.Query().AsNoTracking()
                .Where(e => entryIds.Contains(e.Id))
                .ToDictionaryAsync(e => e.Id, e => e);

            // One entry can have several matching chunks; keep its best-scoring one.
            return matches
                .GroupBy(m => m.EntryId)
                .Where(g => entries.ContainsKey(g.Key))
                .Select(g =>
                {
                    var best = g.OrderByDescending(m => m.Score).First();
                    return (entries[g.Key], best.Score, best.ChunkText.Length > 240 ? best.ChunkText[..240] + "…" : best.ChunkText);
                })
                .OrderByDescending(x => x.Item2)
                .ToList();
        }

        // ---- Attachment ----

        public async Task<AttachmentDto> AddAttachmentAsync(
            Guid userId, Guid spaceId, Guid? nodeId, Guid? entryId,
            string fileName, string contentType, Stream content, string? extractedText)
        {
            await FindSpaceAsync(userId, spaceId);

            if (nodeId is { } nid && !await Nodes.Query().AnyAsync(n => n.Id == nid && n.SpaceId == spaceId))
                throw new KeyNotFoundException("Node not found.");
            if (entryId is { } eid && !await Entries.Query().AnyAsync(e => e.Id == eid && e.SpaceId == spaceId))
                throw new KeyNotFoundException("Entry not found.");

            var stored = await _fileStorage.SaveAsync(userId, $"spaces/{spaceId}", fileName, content);

            var attachment = new Attachment
            {
                Id = Guid.NewGuid(),
                SpaceId = spaceId,
                NodeId = nodeId,
                EntryId = entryId,
                UserId = userId,
                FileName = fileName,
                ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
                SizeBytes = stored.SizeBytes,
                StorageProvider = stored.Provider,
                StoragePath = stored.Path,
                ExtractedText = extractedText,
                CreatedOn = DateTime.UtcNow,
                CreatedBy = userId
            };

            await Attachments.AddAsync(attachment);
            await _unitOfWork.SaveChangesAsync();
            return ToAttachmentDto(attachment);
        }

        public async Task<List<AttachmentDto>> GetAttachmentsAsync(Guid userId, Guid spaceId, Guid? nodeId, Guid? entryId)
        {
            await FindSpaceAsync(userId, spaceId);
            var q = Attachments.Query().AsNoTracking().Where(a => a.SpaceId == spaceId);
            if (nodeId is { } nid) q = q.Where(a => a.NodeId == nid);
            if (entryId is { } eid) q = q.Where(a => a.EntryId == eid);

            var rows = await q.OrderByDescending(a => a.CreatedOn).ToListAsync();
            return rows.Select(ToAttachmentDto).ToList();
        }

        public async Task<(Stream Stream, string ContentType, string FileName)> OpenAttachmentAsync(Guid userId, Guid spaceId, Guid attachmentId)
        {
            await FindSpaceAsync(userId, spaceId);
            var attachment = await Attachments.Query().AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == attachmentId && a.SpaceId == spaceId)
                ?? throw new KeyNotFoundException("Attachment not found.");

            var stream = await _fileStorage.OpenAsync(attachment.StoragePath);
            return (stream, attachment.ContentType, attachment.FileName);
        }

        public async Task DeleteAttachmentAsync(Guid userId, Guid spaceId, Guid attachmentId)
        {
            await FindSpaceAsync(userId, spaceId);
            var attachment = await Attachments.Query().FirstOrDefaultAsync(a => a.Id == attachmentId && a.SpaceId == spaceId)
                ?? throw new KeyNotFoundException("Attachment not found.");

            await _fileStorage.DeleteAsync(attachment.StoragePath);
            Attachments.Delete(attachment);
            await _unitOfWork.SaveChangesAsync();
        }

        // ---- Helpers ----

        private async Task<Space> FindSpaceAsync(Guid userId, Guid spaceId)
        {
            return await Spaces.Query().FirstOrDefaultAsync(s => s.Id == spaceId && s.UserId == userId)
                ?? throw new KeyNotFoundException("Space not found.");
        }

        private string ResolveEntryType(Space space, string requestedType)
        {
            var type = string.IsNullOrWhiteSpace(requestedType) ? "note" : requestedType.Trim();
            try
            {
                var types = JsonSerializer.Deserialize<List<EntryTypeSchema>>(space.SchemaJson) ?? new();
                if (types.Count > 0 && types.All(t => t.Type != type))
                {
                    throw new ArgumentException($"'{type}' is not a valid entry type for this space. Valid types: {string.Join(", ", types.Select(t => t.Type))}.");
                }
            }
            catch (JsonException)
            {
                // Malformed schema shouldn't block entry creation; fall through.
            }
            return type;
        }

        private async Task<string> GenerateUniqueSlugAsync(Guid userId, string name)
        {
            var baseSlug = Slugify(name);
            var slug = baseSlug;
            var i = 1;
            while (await Spaces.Query().AnyAsync(s => s.UserId == userId && s.Slug == slug))
            {
                slug = $"{baseSlug}-{++i}";
            }
            return slug;
        }

        private async Task<string> MakeUniquePathAsync(Guid spaceId, string path, Guid? excludeNodeId = null)
        {
            var candidate = path;
            var i = 1;
            while (await Nodes.Query().AnyAsync(n => n.SpaceId == spaceId && n.Path == candidate && n.Id != excludeNodeId))
            {
                candidate = $"{path}-{++i}";
            }
            return candidate;
        }

        private async Task RepathDescendantsAsync(Guid spaceId, string oldPath, string newPath)
        {
            var prefix = oldPath + "/";
            var descendants = await Nodes.Query().Where(n => n.SpaceId == spaceId && n.Path.StartsWith(prefix)).ToListAsync();
            foreach (var d in descendants)
            {
                d.Path = newPath + d.Path[oldPath.Length..];
            }
            Nodes.UpdateRange(descendants);
        }

        private async Task<IQueryable<Entry>> ApplyNodeFilterAsync(IQueryable<Entry> q, Guid spaceId, Guid? nodeId, bool includeDescendants)
        {
            if (nodeId is not { } nid) return q;

            if (!includeDescendants)
            {
                return q.Where(e => e.NodeId == nid);
            }

            var node = await Nodes.Query().AsNoTracking().FirstOrDefaultAsync(n => n.Id == nid && n.SpaceId == spaceId);
            if (node == null) return q.Where(e => e.NodeId == nid);

            var prefix = node.Path + "/";
            var descendantIds = await Nodes.Query().AsNoTracking()
                .Where(n => n.SpaceId == spaceId && (n.Id == nid || n.Path.StartsWith(prefix)))
                .Select(n => n.Id)
                .ToListAsync();

            return q.Where(e => e.NodeId != null && descendantIds.Contains(e.NodeId.Value));
        }

        private async Task<Dictionary<Guid, string>> GetNodePathsAsync(Guid spaceId, IEnumerable<Guid?> nodeIds)
        {
            var ids = nodeIds.Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
            if (ids.Count == 0) return new Dictionary<Guid, string>();

            return await Nodes.Query().AsNoTracking()
                .Where(n => n.SpaceId == spaceId && ids.Contains(n.Id))
                .ToDictionaryAsync(n => n.Id, n => n.Path);
        }

        private async Task<Dictionary<(Guid SpaceId, Guid NodeId), string>> GetNodePathsAsync(IEnumerable<(Guid SpaceId, Guid? NodeId)> pairs)
        {
            var bySpace = pairs.Where(p => p.NodeId.HasValue).GroupBy(p => p.SpaceId);
            var result = new Dictionary<(Guid, Guid), string>();
            foreach (var group in bySpace)
            {
                var ids = group.Select(p => p.NodeId!.Value).Distinct().ToList();
                var map = await Nodes.Query().AsNoTracking()
                    .Where(n => n.SpaceId == group.Key && ids.Contains(n.Id))
                    .ToDictionaryAsync(n => n.Id, n => n.Path);
                foreach (var kv in map) result[(group.Key, kv.Key)] = kv.Value;
            }
            return result;
        }

        private static string Slugify(string value)
        {
            var lowered = value.Trim().ToLowerInvariant();
            var chars = lowered.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray();
            var slug = new string(chars);
            while (slug.Contains("--")) slug = slug.Replace("--", "-");
            return slug.Trim('-');
        }

        private static string[] NormalizeTags(List<string>? tags)
        {
            if (tags is null) return Array.Empty<string>();
            return tags.Select(t => t.Trim().ToLowerInvariant()).Where(t => t.Length > 0).Distinct().ToArray();
        }

        private static string ValidateJson(string json, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(json)) return "{}";
            try
            {
                using var doc = JsonDocument.Parse(json);
                return json;
            }
            catch (JsonException)
            {
                throw new ArgumentException($"Invalid JSON for {fieldName}.");
            }
        }

        private static string MergeJson(string baseJson, string patchJson)
        {
            var baseDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(string.IsNullOrWhiteSpace(baseJson) ? "{}" : baseJson) ?? new();
            var patchDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(string.IsNullOrWhiteSpace(patchJson) ? "{}" : patchJson) ?? new();
            foreach (var kv in patchDict) baseDict[kv.Key] = kv.Value;
            return JsonSerializer.Serialize(baseDict);
        }

        private static string BuildSnippet(string body, string term)
        {
            if (string.IsNullOrEmpty(body)) return string.Empty;
            var idx = body.IndexOf(term, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return body.Length > 240 ? body[..240] + "…" : body;

            var start = Math.Max(0, idx - 100);
            var length = Math.Min(240, body.Length - start);
            var snippet = body.Substring(start, length);
            return (start > 0 ? "…" : "") + snippet + (start + length < body.Length ? "…" : "");
        }

        private static SpaceDto ToSpaceDto(Space s) => new()
        {
            Id = s.Id,
            Name = s.Name,
            Slug = s.Slug,
            Kind = s.Kind,
            Description = s.Description,
            Conventions = s.Conventions,
            SettingsJson = s.SettingsJson,
            StateJson = s.StateJson,
            SchemaJson = s.SchemaJson,
            Status = s.Status,
            CreatedOn = s.CreatedOn,
            UpdatedOn = s.UpdatedOn
        };

        private static NodeDto ToNodeDto(Node n, Dictionary<Guid, int> entryCounts, Dictionary<Guid, int> childCounts) => new()
        {
            Id = n.Id,
            SpaceId = n.SpaceId,
            ParentId = n.ParentId,
            Name = n.Name,
            Path = n.Path,
            SortOrder = n.SortOrder,
            Kind = n.Kind,
            Summary = n.Summary,
            EntryCount = entryCounts.GetValueOrDefault(n.Id),
            ChildCount = childCounts.GetValueOrDefault(n.Id)
        };

        private static EntryDto ToEntryDto(Entry e, Dictionary<Guid, string> nodePaths, int attachmentCount) => new()
        {
            Id = e.Id,
            SpaceId = e.SpaceId,
            NodeId = e.NodeId,
            NodePath = e.NodeId is { } nid ? nodePaths.GetValueOrDefault(nid) : null,
            Type = e.Type,
            Title = e.Title,
            Body = e.Body,
            FieldsJson = e.FieldsJson,
            Tags = e.Tags.ToList(),
            OccurredOn = e.OccurredOn,
            DueOn = e.DueOn,
            Status = e.Status,
            Source = e.Source,
            CreatedOn = e.CreatedOn,
            UpdatedOn = e.UpdatedOn,
            AttachmentCount = attachmentCount
        };

        private static EntryListItemDto ToListItemDto(Entry e, Dictionary<Guid, string> nodePaths) => new()
        {
            Id = e.Id,
            NodeId = e.NodeId,
            NodePath = e.NodeId is { } nid ? nodePaths.GetValueOrDefault(nid) : null,
            Type = e.Type,
            Title = e.Title,
            Excerpt = e.Body.Length > 200 ? e.Body[..200] + "…" : e.Body,
            Tags = e.Tags.ToList(),
            OccurredOn = e.OccurredOn,
            DueOn = e.DueOn,
            Status = e.Status,
            Source = e.Source,
            CreatedOn = e.CreatedOn
        };

        private static SearchResultDto ToSearchResult(Entry e, Dictionary<(Guid, Guid), string> nodePaths, double rank, string snippet) => new()
        {
            EntryId = e.Id,
            SpaceId = e.SpaceId,
            SpaceName = e.Space?.Name ?? string.Empty,
            NodeId = e.NodeId,
            NodePath = e.NodeId is { } nid ? nodePaths.GetValueOrDefault((e.SpaceId, nid)) : null,
            Type = e.Type,
            Title = e.Title,
            Snippet = snippet,
            OccurredOn = e.OccurredOn,
            Rank = rank
        };

        private static AttachmentDto ToAttachmentDto(Attachment a) => new()
        {
            Id = a.Id,
            SpaceId = a.SpaceId,
            NodeId = a.NodeId,
            EntryId = a.EntryId,
            FileName = a.FileName,
            ContentType = a.ContentType,
            SizeBytes = a.SizeBytes,
            StorageProvider = a.StorageProvider,
            CreatedOn = a.CreatedOn
        };
    }
}
