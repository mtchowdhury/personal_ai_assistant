using System;
using System.ComponentModel.DataAnnotations.Schema;
using CmdNext.Models.Domain.DTOs.Constants;
using CmdNext.Models.Domain.Model.Abstraction;

namespace CmdNext.Models.Domain.Model.App.Spaces
{
    /// <summary>A file attached to a node or an entry (handout, photo, signed contract, ...).</summary>
    [Table("Attachments", Schema = DBSchema.Spaces)]
    public class Attachment : BaseEntity<Guid>
    {
        public Guid SpaceId { get; set; }

        public Space? Space { get; set; }

        public Guid? NodeId { get; set; }

        public Node? Node { get; set; }

        public Guid? EntryId { get; set; }

        public Entry? Entry { get; set; }

        public Guid UserId { get; set; }

        public string FileName { get; set; } = string.Empty;

        public string ContentType { get; set; } = "application/octet-stream";

        public long SizeBytes { get; set; }

        /// <summary>"local" now; "onedrive"/"gdrive" later.</summary>
        public string StorageProvider { get; set; } = "local";

        /// <summary>Relative path inside the storage provider.</summary>
        public string StoragePath { get; set; } = string.Empty;

        /// <summary>Markdown/plain text extracted from the file (PDF/Word/Excel), if any.</summary>
        public string? ExtractedText { get; set; }
    }
}
