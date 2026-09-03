using System;
using System.ComponentModel.DataAnnotations.Schema;
using CmdNext.Models.Domain.DTOs.Constants;
using CmdNext.Models.Domain.Model.Abstraction;

namespace CmdNext.Models.Domain.Model.App.Spaces
{
    /// <summary>
    /// A personal project space (e.g. a language course, an instrument, a people journal, a
    /// house move). Owns a tree of <see cref="Node"/>s and a set of <see cref="Entry"/> documents.
    /// </summary>
    [Table("Spaces", Schema = DBSchema.Spaces)]
    public class Space : BaseEntity<Guid>
    {
        public Guid UserId { get; set; }

        public string Name { get; set; } = string.Empty;

        /// <summary>URL-safe, unique per user.</summary>
        public string Slug { get; set; } = string.Empty;

        /// <summary>Template key, e.g. "learning", "journal", "people", "process", "custom".</summary>
        public string Kind { get; set; } = "custom";

        public string? Description { get; set; }

        /// <summary>
        /// AI system-prompt addendum for this space — conventions, tone, what to do with
        /// attachments, etc. (equivalent to the Cursor rule / README in the old folder-based setup).
        /// </summary>
        public string? Conventions { get; set; }

        /// <summary>Free-form per-space configuration, e.g. {"vocabsPerClass":15}.</summary>
        public string SettingsJson { get; set; } = "{}";

        /// <summary>Progress state the AI/user updates, e.g. {"lastCompleted":"...","next":"..."}.</summary>
        public string StateJson { get; set; } = "{}";

        /// <summary>Entry-type field definitions for this space, copied from the template at creation.</summary>
        public string SchemaJson { get; set; } = "{}";

        /// <summary>"active" or "archived".</summary>
        public string Status { get; set; } = "active";
    }
}
