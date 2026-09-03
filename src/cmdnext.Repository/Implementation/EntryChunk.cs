using System;
using System.ComponentModel.DataAnnotations.Schema;
using CmdNext.Models.Domain.Model.Abstraction;
using CmdNext.Models.Domain.Model.App.Spaces;
using Pgvector;

namespace CmdNext.Repository.Implementation
{
    /// <summary>
    /// One embedded chunk of an <see cref="Entry"/>'s body (or an <see cref="Attachment"/>'s
    /// extracted text), used for semantic search. A long entry is split into overlapping
    /// chunks rather than embedded as a single vector, so a specific paragraph can match
    /// without averaging into the whole document. Lives in the Repository project (not
    /// Models) because it carries a pgvector-typed column and is purely a search index —
    /// never surfaced through a DTO.
    /// </summary>
    [Table("EntryChunks", Schema = "spaces")]
    public class EntryChunk : BaseEntity<Guid>
    {
        public Guid EntryId { get; set; }

        public Entry? Entry { get; set; }

        public Guid SpaceId { get; set; }

        public int ChunkIndex { get; set; }

        public string Text { get; set; } = string.Empty;

        /// <summary>Null until embedded; embedding is best-effort and never blocks entry writes.</summary>
        public Vector? Embedding { get; set; }
    }
}
