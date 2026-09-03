using System;
using System.Collections.Generic;

namespace CmdNext.Models.Domain.DTOs.Spaces
{
    // ---- Templates ----

    /// <summary>A field definition inside an entry type's schema.</summary>
    public class FieldSchema
    {
        public string Name { get; set; } = string.Empty;
        /// <summary>"text" | "richtext" | "date" | "number" | "bool" | "select" | "tags".</summary>
        public string Type { get; set; } = "text";
        public bool Required { get; set; }
        public List<string>? Options { get; set; }
    }

    /// <summary>An entry type available in a space, with its field schema.</summary>
    public class EntryTypeSchema
    {
        public string Type { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public List<FieldSchema> Fields { get; set; } = new();
    }

    /// <summary>A built-in space template: suggested node kind + the entry types it ships with.</summary>
    public class SpaceTemplateDto
    {
        public string Kind { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? SuggestedNodeKind { get; set; }
        public List<EntryTypeSchema> EntryTypes { get; set; } = new();
    }

    // ---- Space ----

    public class SpaceDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Conventions { get; set; }
        public string SettingsJson { get; set; } = "{}";
        public string StateJson { get; set; } = "{}";
        public string SchemaJson { get; set; } = "{}";
        public string Status { get; set; } = "active";
        public DateTime? CreatedOn { get; set; }
        public DateTime? UpdatedOn { get; set; }
    }

    public class CreateSpaceRequest
    {
        public string Name { get; set; } = string.Empty;
        /// <summary>Template kind key; determines the seeded entry-type schema.</summary>
        public string Kind { get; set; } = "custom";
        public string? Description { get; set; }
        public string? Conventions { get; set; }
    }

    public class UpdateSpaceRequest
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Conventions { get; set; }
        public string? SettingsJson { get; set; }
        public string? Status { get; set; }
    }

    /// <summary>Merges (shallow) into the space's StateJson.</summary>
    public class UpdateSpaceStateRequest
    {
        public string StatePatchJson { get; set; } = "{}";
    }

    public class UpdateSpaceSchemaRequest
    {
        public string SchemaJson { get; set; } = "{}";
    }

    // ---- Node ----

    public class NodeDto
    {
        public Guid Id { get; set; }
        public Guid SpaceId { get; set; }
        public Guid? ParentId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public string? Kind { get; set; }
        public string? Summary { get; set; }
        public int EntryCount { get; set; }
        public int ChildCount { get; set; }
    }

    public class CreateNodeRequest
    {
        public Guid? ParentId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Kind { get; set; }
        public string? Summary { get; set; }
    }

    public class UpdateNodeRequest
    {
        public string? Name { get; set; }
        public Guid? ParentId { get; set; }
        public bool ParentIdSet { get; set; }
        public string? Summary { get; set; }
        public int? SortOrder { get; set; }
    }

    // ---- Entry ----

    public class EntryDto
    {
        public Guid Id { get; set; }
        public Guid SpaceId { get; set; }
        public Guid? NodeId { get; set; }
        public string? NodePath { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string FieldsJson { get; set; } = "{}";
        public List<string> Tags { get; set; } = new();
        public DateTime? OccurredOn { get; set; }
        public DateTime? DueOn { get; set; }
        public string? Status { get; set; }
        public string Source { get; set; } = "manual";
        public DateTime? CreatedOn { get; set; }
        public DateTime? UpdatedOn { get; set; }
        public int AttachmentCount { get; set; }
    }

    public class EntryListItemDto
    {
        public Guid Id { get; set; }
        public Guid? NodeId { get; set; }
        public string? NodePath { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        /// <summary>First ~200 chars of Body, for list views.</summary>
        public string Excerpt { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new();
        public DateTime? OccurredOn { get; set; }
        public DateTime? DueOn { get; set; }
        public string? Status { get; set; }
        public string Source { get; set; } = "manual";
        public DateTime? CreatedOn { get; set; }
    }

    public class CreateEntryRequest
    {
        public Guid? NodeId { get; set; }
        public string Type { get; set; } = "note";
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        /// <summary>JSON object matching the space's field schema for this Type.</summary>
        public string? FieldsJson { get; set; }
        public List<string>? Tags { get; set; }
        public DateTime? OccurredOn { get; set; }
        public DateTime? DueOn { get; set; }
        public string? Status { get; set; }
        /// <summary>"ai" or "manual"; defaults to manual.</summary>
        public string? Source { get; set; }
    }

    public class UpdateEntryRequest
    {
        public Guid? NodeId { get; set; }
        public bool NodeIdSet { get; set; }
        public string? Title { get; set; }
        public string? Body { get; set; }
        public string? FieldsJson { get; set; }
        public List<string>? Tags { get; set; }
        public DateTime? OccurredOn { get; set; }
        public DateTime? DueOn { get; set; }
        public string? Status { get; set; }
    }

    /// <summary>Appends a dated markdown section to an entry's body; never replaces existing content.</summary>
    public class AppendToEntryRequest
    {
        public string Text { get; set; } = string.Empty;
        /// <summary>Optional heading for the appended section, e.g. "Follow-up 2026-09-10".</summary>
        public string? Heading { get; set; }
    }

    public class EntryQuery
    {
        public Guid? NodeId { get; set; }
        /// <summary>When true with NodeId set, includes entries under descendant nodes too.</summary>
        public bool IncludeDescendants { get; set; }
        public string? Type { get; set; }
        public List<string>? Tags { get; set; }
        /// <summary>Exact-match field filters, e.g. {"person":"X"}.</summary>
        public Dictionary<string, string>? Fields { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public string? Status { get; set; }
        public int Take { get; set; } = 100;
    }

    // ---- Search ----

    public class SearchEntriesRequest
    {
        public Guid? SpaceId { get; set; }
        public Guid? NodeId { get; set; }
        public bool IncludeDescendants { get; set; } = true;
        public string? Query { get; set; }
        public string? Type { get; set; }
        public List<string>? Tags { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        /// <summary>
        /// "hybrid" (default) runs text + semantic and merges; "text" is full-text/trigram only;
        /// "semantic" is embedding similarity only. Semantic modes return nothing (not an error)
        /// when no embedding API key is configured — callers still get text results in hybrid mode.
        /// </summary>
        public string Mode { get; set; } = "hybrid";
        public int Limit { get; set; } = 20;
    }

    public class SearchResultDto
    {
        public Guid EntryId { get; set; }
        public Guid SpaceId { get; set; }
        public string SpaceName { get; set; } = string.Empty;
        public Guid? NodeId { get; set; }
        public string? NodePath { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Snippet { get; set; } = string.Empty;
        public DateTime? OccurredOn { get; set; }
        public double Rank { get; set; }
    }

    // ---- Attachment ----

    public class AttachmentDto
    {
        public Guid Id { get; set; }
        public Guid SpaceId { get; set; }
        public Guid? NodeId { get; set; }
        public Guid? EntryId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public string StorageProvider { get; set; } = "local";
        public DateTime? CreatedOn { get; set; }
    }
}
