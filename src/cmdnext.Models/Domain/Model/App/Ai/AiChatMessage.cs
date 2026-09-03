using System;
using System.ComponentModel.DataAnnotations.Schema;
using CmdNext.Models.Domain.DTOs.Constants;
using CmdNext.Models.Domain.Model.Abstraction;

namespace CmdNext.Models.Domain.Model.App.Ai
{
    [Table("AiChatSessionMessage", Schema = DBSchema.Ai)]
    public class AiChatMessage : BaseEntity<Guid>
    {
        public Guid SessionId { get; set; }

        public AiChatSession? Session { get; set; }

        public string Role { get; set; } = string.Empty;

        public string? Content { get; set; }

        public int Sequence { get; set; }

        public string? ToolCallsJson { get; set; }

        public string? AttachmentsJson { get; set; }

        public string? Provider { get; set; }

        public string? Model { get; set; }

        public int? InputTokens { get; set; }

        public int? OutputTokens { get; set; }

        public string? FinishReason { get; set; }

        public bool IsError { get; set; }
    }
}
