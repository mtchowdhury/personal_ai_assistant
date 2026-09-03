using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using CmdNext.Models.Domain.DTOs.Ai;
using CmdNext.Models.Domain.DTOs.Spaces;
using CmdNext.Service.Contracts;

namespace CmdNext.Service.Tools
{
    /// <summary>
    /// AI-callable tools for Spaces. Read + add/append only, matching the finance tools'
    /// safety policy — no delete, no body overwrite. <see cref="SaveAttachmentAsync"/> is the
    /// one exception worth calling out: it must be called only when the user explicitly asked
    /// to keep/save/file an attachment in that message. Most attachments in a space-scoped
    /// chat are one-off reference material for that reply and are never persisted as an
    /// Attachment row — the tool exists so a deliberate "save this" has somewhere to go, not
    /// so every image gets filed automatically.
    /// </summary>
    public class SpacesAiTools
    {
        private readonly ISpaceService _spaces;
        private readonly ILogger _logger;
        private readonly Guid _userId;
        private readonly Guid? _defaultSpaceId;
        private readonly IReadOnlyList<AiChatMessageAttachment> _pendingAttachments;

        public SpacesAiTools(
            ISpaceService spaces, ILogger logger, Guid userId, Guid? defaultSpaceId,
            IReadOnlyList<AiChatMessageAttachment> pendingAttachments)
        {
            _spaces = spaces;
            _logger = logger;
            _userId = userId;
            _defaultSpaceId = defaultSpaceId;
            _pendingAttachments = pendingAttachments;
        }

        public async Task<IReadOnlyList<AITool>> GetToolsAsync()
        {
            var tools = new List<AITool>
            {
                AIFunctionFactory.Create(ListSpacesAsync, "list_spaces",
                    "List the user's personal project spaces (id, name, kind, status, one-line progress state)."),
                AIFunctionFactory.Create(GetSpaceAsync, "get_space",
                    "Get one space's conventions, settings, progress state, node tree, and entry types. " +
                    "Call this before adding entries so field names match the space's schema."),
                AIFunctionFactory.Create(SearchEntriesAsync, "search_entries",
                    "Search entries by free text (title/body full-text, falls back to fuzzy matching), " +
                    "optionally scoped to one space and/or node subtree, filtered by type/tags/date range."),
                AIFunctionFactory.Create(GetEntryAsync, "get_entry",
                    "Get one entry's full body, fields, tags, and attachment list."),
                AIFunctionFactory.Create(ListEntriesAsync, "list_entries",
                    "List entries in a space, optionally under one node (with descendants), filtered by type/date/status."),
                AIFunctionFactory.Create(AddEntryAsync, "add_entry",
                    "Add a new entry (note, journal entry, vocab word, event, task, ...) to a space. " +
                    "The 'type' must be one of the space's entry types from get_space; 'fields' should match that type's field schema."),
                AIFunctionFactory.Create(AppendToEntryAsync, "append_to_entry",
                    "Append a new dated section to an existing entry's body. Never replaces or edits existing content."),
                AIFunctionFactory.Create(AddNodeAsync, "add_node",
                    "Create a new node (folder) in a space's tree, optionally under a parent node."),
                AIFunctionFactory.Create(UpdateSpaceStateAsync, "update_space_state",
                    "Merge fields into a space's progress state (e.g. lastCompleted, next) after completing a step."),
                AIFunctionFactory.Create(SaveAttachmentAsync, "save_attachment",
                    "Save one of the files attached to the CURRENT message into a space, as a material on a node " +
                    "and/or an entry. Call this ONLY when the user explicitly asked to save, keep, or file the " +
                    "attachment (e.g. 'save this', 'keep this as a material', 'file this under class 12'). " +
                    "Do not call it just because an image or file was attached — most attachments are for this " +
                    "reply only and should not be saved.")
            };

            if (_defaultSpaceId is { } spaceId)
            {
                var space = await TryGetSpaceAsync(spaceId);
                if (space?.Kind == "people")
                {
                    tools.Add(AIFunctionFactory.Create(
                        (string? person) => PeopleReportAsync(spaceId, person),
                        "people_report",
                        "For a 'people' space: summarize contact history for one person node (by name) or all of " +
                        "them — last contact date, contact count, and how often they initiated vs. only reached out " +
                        "when they needed something. Use this before answering questions like 'who only contacts me " +
                        "when they need something' or 'when did I last see X and how did that go'."));
                }
            }

            return tools;
        }

        // ---- Tool implementations ----

        private async Task<string> ListSpacesAsync()
        {
            var spaces = await _spaces.GetSpacesAsync(_userId);
            if (spaces.Count == 0) return "No spaces yet.";

            return string.Join("\n", spaces.Select(s =>
                $"- {s.Name} (id: {s.Id}, kind: {s.Kind}) — {StateLine(s.StateJson)}"));
        }

        private async Task<string> GetSpaceAsync(
            [Description("Space id from list_spaces.")] string spaceId)
        {
            var id = ParseGuid(spaceId, nameof(spaceId));
            var space = await _spaces.GetSpaceAsync(_userId, id);
            var nodes = await _spaces.GetNodesAsync(_userId, id);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Space: {space.Name} (kind: {space.Kind})");
            if (!string.IsNullOrWhiteSpace(space.Conventions))
                sb.AppendLine($"Conventions: {space.Conventions}");
            sb.AppendLine($"State: {space.StateJson}");
            sb.AppendLine($"Entry types (schema): {space.SchemaJson}");
            sb.AppendLine(nodes.Count == 0
                ? "Node tree: (empty)"
                : "Node tree:\n" + string.Join("\n", nodes.Select(n => $"  {n.Path} (id: {n.Id}, {n.EntryCount} entries)")));

            return sb.ToString();
        }

        private async Task<string> SearchEntriesAsync(
            [Description("Free text to search for.")] string query,
            [Description("Restrict to one space id. Omit to search across all the user's spaces.")] string? spaceId,
            [Description("Restrict to one node id and its descendants.")] string? nodeId,
            [Description("Restrict to one entry type.")] string? type,
            [Description("Limit, default 20.")] int? limit)
        {
            var results = await _spaces.SearchAsync(_userId, new SearchEntriesRequest
            {
                SpaceId = string.IsNullOrWhiteSpace(spaceId) ? _defaultSpaceId : ParseGuid(spaceId, nameof(spaceId)),
                NodeId = string.IsNullOrWhiteSpace(nodeId) ? null : ParseGuid(nodeId, nameof(nodeId)),
                IncludeDescendants = true,
                Query = query,
                Type = type,
                Limit = limit ?? 20
            });

            if (results.Count == 0) return "No matching entries.";

            return string.Join("\n", results.Select(r =>
                $"- [{r.SpaceName}{(r.NodePath != null ? "/" + r.NodePath : "")}] {r.Title} (entry id: {r.EntryId}, type: {r.Type}) — {r.Snippet}"));
        }

        private async Task<string> GetEntryAsync(
            [Description("Space id.")] string spaceId,
            [Description("Entry id.")] string entryId)
        {
            var entry = await _spaces.GetEntryAsync(_userId, ParseGuid(spaceId, nameof(spaceId)), ParseGuid(entryId, nameof(entryId)));
            return $"Title: {entry.Title}\nType: {entry.Type}\nTags: {string.Join(", ", entry.Tags)}\n" +
                   $"Occurred: {entry.OccurredOn:yyyy-MM-dd}\nFields: {entry.FieldsJson}\nAttachments: {entry.AttachmentCount}\n\n{entry.Body}";
        }

        private async Task<string> ListEntriesAsync(
            [Description("Space id.")] string spaceId,
            [Description("Restrict to one node id (includes descendants).")] string? nodeId,
            [Description("Restrict to one entry type.")] string? type,
            [Description("Max results, default 50.")] int? take)
        {
            var id = ParseGuid(spaceId, nameof(spaceId));
            var entries = await _spaces.GetEntriesAsync(_userId, id, new EntryQuery
            {
                NodeId = string.IsNullOrWhiteSpace(nodeId) ? null : ParseGuid(nodeId, nameof(nodeId)),
                IncludeDescendants = true,
                Type = type,
                Take = take ?? 50
            });

            if (entries.Count == 0) return "No entries found.";

            return string.Join("\n", entries.Select(e =>
                $"- {e.Title} (id: {e.Id}, type: {e.Type}{(e.OccurredOn is { } d ? ", " + d.ToString("yyyy-MM-dd") : "")}) — {e.Excerpt}"));
        }

        private async Task<string> AddEntryAsync(
            [Description("Space id.")] string spaceId,
            [Description("Node id to attach this entry to. Omit for a space-level entry.")] string? nodeId,
            [Description("Entry type — must be one of the space's entry types from get_space.")] string type,
            [Description("Entry title.")] string title,
            [Description("Markdown body.")] string body,
            [Description("JSON object of field values matching the entry type's field schema, e.g. {\"word\":\"Wohnung\",\"article\":\"die\"}.")] string? fieldsJson,
            [Description("Comma-separated tags.")] string? tags,
            [Description("Date this entry is about, yyyy-MM-dd. Defaults to none.")] string? occurredOn)
        {
            var id = ParseGuid(spaceId, nameof(spaceId));
            var entry = await _spaces.CreateEntryAsync(_userId, id, new CreateEntryRequest
            {
                NodeId = string.IsNullOrWhiteSpace(nodeId) ? null : ParseGuid(nodeId, nameof(nodeId)),
                Type = type,
                Title = title,
                Body = body,
                FieldsJson = fieldsJson,
                Tags = ParseTags(tags),
                OccurredOn = TryDate(occurredOn),
                Source = "ai"
            });

            _logger.LogInformation("AI tool add_entry created {EntryId} in space {SpaceId}", entry.Id, id);
            return $"Created entry '{entry.Title}' (id: {entry.Id}).";
        }

        private async Task<string> AppendToEntryAsync(
            [Description("Space id.")] string spaceId,
            [Description("Entry id to append to.")] string entryId,
            [Description("Markdown text to append as a new dated section.")] string text,
            [Description("Optional heading for the new section.")] string? heading)
        {
            var entry = await _spaces.AppendToEntryAsync(
                _userId, ParseGuid(spaceId, nameof(spaceId)), ParseGuid(entryId, nameof(entryId)),
                new AppendToEntryRequest { Text = text, Heading = heading });

            return $"Appended to '{entry.Title}'.";
        }

        private async Task<string> AddNodeAsync(
            [Description("Space id.")] string spaceId,
            [Description("Node name.")] string name,
            [Description("Parent node id. Omit for a top-level node.")] string? parentId,
            [Description("Optional short label, e.g. 'class', 'person', 'lecture'.")] string? kind)
        {
            var id = ParseGuid(spaceId, nameof(spaceId));
            var node = await _spaces.CreateNodeAsync(_userId, id, new CreateNodeRequest
            {
                ParentId = string.IsNullOrWhiteSpace(parentId) ? null : ParseGuid(parentId, nameof(parentId)),
                Name = name,
                Kind = kind
            });

            return $"Created node '{node.Name}' at {node.Path} (id: {node.Id}).";
        }

        private async Task<string> UpdateSpaceStateAsync(
            [Description("Space id.")] string spaceId,
            [Description("JSON object to merge into the space's state, e.g. {\"lastCompleted\":\"class-12\",\"next\":\"class-13\"}.")] string statePatchJson)
        {
            var space = await _spaces.UpdateSpaceStateAsync(_userId, ParseGuid(spaceId, nameof(spaceId)), new UpdateSpaceStateRequest { StatePatchJson = statePatchJson });
            return $"State updated: {space.StateJson}";
        }

        private async Task<string> SaveAttachmentAsync(
            [Description("Space id.")] string spaceId,
            [Description("Exact file name of the attachment from the current message to save.")] string fileName,
            [Description("Node id to save it under. Omit if saving to an entry instead.")] string? nodeId,
            [Description("Entry id to save it under. Omit if saving to a node instead.")] string? entryId)
        {
            var attachment = _pendingAttachments.FirstOrDefault(a =>
                string.Equals(a.FileName, fileName, StringComparison.OrdinalIgnoreCase));

            if (attachment == null)
            {
                return $"No attachment named '{fileName}' on the current message. " +
                       $"Available: {(_pendingAttachments.Count == 0 ? "(none)" : string.Join(", ", _pendingAttachments.Select(a => a.FileName)))}.";
            }

            var id = ParseGuid(spaceId, nameof(spaceId));
            var nId = string.IsNullOrWhiteSpace(nodeId) ? (Guid?)null : ParseGuid(nodeId, nameof(nodeId));
            var eId = string.IsNullOrWhiteSpace(entryId) ? (Guid?)null : ParseGuid(entryId, nameof(entryId));

            byte[] bytes;
            string contentType;
            string? extractedText = null;
            var storedFileName = attachment.FileName;

            if (!string.IsNullOrEmpty(attachment.Data))
            {
                bytes = Convert.FromBase64String(attachment.Data);
                contentType = attachment.MediaType ?? "application/octet-stream";
            }
            else
            {
                // Non-image attachments (PDF/DOCX/XLSX) were already converted to markdown for
                // the model and the original bytes are gone by this point — save the extracted
                // text itself, under a .md name so the stored file's extension/content-type
                // actually match what's on disk (the original name is kept in ExtractedText's
                // heading below and in the tool's return value for the user's benefit).
                bytes = System.Text.Encoding.UTF8.GetBytes(attachment.Markdown ?? string.Empty);
                contentType = "text/markdown";
                extractedText = attachment.Markdown;
                storedFileName = Path.GetFileNameWithoutExtension(attachment.FileName) + ".md";
            }

            using var stream = new MemoryStream(bytes);
            var saved = await _spaces.AddAttachmentAsync(_userId, id, nId, eId, storedFileName, contentType, stream, extractedText);

            _logger.LogInformation("AI tool save_attachment saved {AttachmentId} ({FileName}) to space {SpaceId}", saved.Id, saved.FileName, id);
            return $"Saved '{saved.FileName}' (id: {saved.Id}).";
        }

        private async Task<string> PeopleReportAsync(Guid spaceId, string? personName)
        {
            var nodes = await _spaces.GetNodesAsync(_userId, spaceId);
            var people = string.IsNullOrWhiteSpace(personName)
                ? nodes
                : nodes.Where(n => n.Name.Contains(personName, StringComparison.OrdinalIgnoreCase)).ToList();

            if (people.Count == 0) return $"No person node matching '{personName}'.";

            var sb = new System.Text.StringBuilder();
            foreach (var person in people)
            {
                var events = await _spaces.GetEntriesAsync(_userId, spaceId, new EntryQuery
                {
                    NodeId = person.Id,
                    IncludeDescendants = false,
                    Type = "event",
                    Take = 500
                });

                if (events.Count == 0)
                {
                    sb.AppendLine($"{person.Name}: no recorded events.");
                    continue;
                }

                var withDates = events.Where(e => e.OccurredOn.HasValue).OrderByDescending(e => e.OccurredOn).ToList();
                var last = withDates.FirstOrDefault();

                var initiatedByThem = 0;
                var total = 0;
                foreach (var e in events)
                {
                    var full = await _spaces.GetEntryAsync(_userId, spaceId, e.Id);
                    try
                    {
                        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(full.FieldsJson) ? "{}" : full.FieldsJson);
                        if (doc.RootElement.TryGetProperty("initiatedBy", out var initiatedBy))
                        {
                            total++;
                            if (string.Equals(initiatedBy.GetString(), "them", StringComparison.OrdinalIgnoreCase))
                                initiatedByThem++;
                        }
                    }
                    catch (JsonException) { }
                }

                sb.AppendLine($"{person.Name}: {events.Count} event(s), last on {last?.OccurredOn:yyyy-MM-dd}" +
                    (total > 0 ? $", they initiated {initiatedByThem}/{total} of tracked contacts" : "") + ".");
            }

            return sb.ToString();
        }

        // ---- Helpers ----

        private async Task<SpaceDto?> TryGetSpaceAsync(Guid spaceId)
        {
            try { return await _spaces.GetSpaceAsync(_userId, spaceId); }
            catch (KeyNotFoundException) { return null; }
        }

        private static string StateLine(string stateJson)
        {
            try
            {
                using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(stateJson) ? "{}" : stateJson);
                if (doc.RootElement.TryGetProperty("next", out var next)) return $"next: {next}";
                if (doc.RootElement.TryGetProperty("lastCompleted", out var last)) return $"last: {last}";
            }
            catch (JsonException) { }
            return "no progress recorded";
        }

        private static Guid ParseGuid(string value, string paramName)
        {
            if (!Guid.TryParse(value, out var id))
                throw new ArgumentException($"'{value}' is not a valid id for {paramName}.");
            return id;
        }

        private static DateTime? TryDate(string? value)
        {
            if (!string.IsNullOrWhiteSpace(value)
                && DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var d))
            {
                return d;
            }
            return null;
        }

        private static List<string>? ParseTags(string? tags) =>
            string.IsNullOrWhiteSpace(tags)
                ? null
                : tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }
}
