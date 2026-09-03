using System;
using System.ComponentModel.DataAnnotations.Schema;
using CmdNext.Models.Domain.DTOs.Constants;
using CmdNext.Models.Domain.Model.Abstraction;

namespace CmdNext.Models.Domain.Model.App.Spaces
{
    /// <summary>
    /// A document inside a space: a note, journal entry, vocab word, event, task, etc. Body is
    /// free markdown; <see cref="FieldsJson"/> carries the typed facets defined by the space's
    /// entry-type schema so filtering/aggregation doesn't require parsing prose.
    /// </summary>
    [Table("Entries", Schema = DBSchema.Spaces)]
    public class Entry : BaseEntity<Guid>
    {
        public Guid SpaceId { get; set; }

        public Space? Space { get; set; }

        /// <summary>Null means a space-level entry (not attached to any node).</summary>
        public Guid? NodeId { get; set; }

        public Node? Node { get; set; }

        public Guid UserId { get; set; }

        /// <summary>Entry type key from the space's schema, e.g. "note", "vocab", "event", "task".</summary>
        public string Type { get; set; } = "note";

        public string Title { get; set; } = string.Empty;

        public string Body { get; set; } = string.Empty;

        /// <summary>Typed facets per <see cref="Type"/>, e.g. {"word":"Wohnung","article":"die"}.</summary>
        public string FieldsJson { get; set; } = "{}";

        public string[] Tags { get; set; } = Array.Empty<string>();

        /// <summary>The event date this entry is about (class date, meeting date) — distinct from CreatedOn.</summary>
        public DateTime? OccurredOn { get; set; }

        /// <summary>For task-like entries.</summary>
        public DateTime? DueOn { get; set; }

        /// <summary>Free status per type, e.g. "open"/"done", "sold"/"listed".</summary>
        public string? Status { get; set; }

        /// <summary>"ai" or "manual".</summary>
        public string Source { get; set; } = "manual";

        public DateTime? DeletedOn { get; set; }

        // Note: a generated `SearchVector tsvector` column (GENERATED ALWAYS AS ... STORED over
        // Title + Body) is added directly in the migration and queried via raw SQL / EF.Functions
        // in SpaceService; it is intentionally not mapped as a CLR property here since the
        // Models project stays free of Npgsql-specific types.
    }
}
