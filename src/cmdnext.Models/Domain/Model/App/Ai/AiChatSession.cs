using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using CmdNext.Models.Domain.DTOs.Constants;
using CmdNext.Models.Domain.Model.Abstraction;

namespace CmdNext.Models.Domain.Model.App.Ai
{
    [Table("AiChatSession", Schema = DBSchema.Ai)]
    public class AiChatSession : BaseEntity<Guid>
    {
        public Guid UserId { get; set; }

        public string? Title { get; set; }

        public string? ProfileName { get; set; }

        public string? Provider { get; set; }

        public string? Model { get; set; }

        public string? SummaryText { get; set; }

        public Guid? SummarizedUpToMessageId { get; set; }

        public DateTime? LastMessageAt { get; set; }

        public bool IsArchived { get; set; }

        public ICollection<AiChatMessage>? Messages { get; set; }
    }
}
