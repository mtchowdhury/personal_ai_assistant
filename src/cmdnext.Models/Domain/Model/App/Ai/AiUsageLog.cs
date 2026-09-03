using System;
using System.ComponentModel.DataAnnotations.Schema;
using CmdNext.Models.Domain.DTOs.Constants;
using CmdNext.Models.Domain.Model.Abstraction;

namespace CmdNext.Models.Domain.Model.App.Ai
{
    [Table("AiUsageLog", Schema = DBSchema.Ai)]
    public class AiUsageLog : BaseEntity<Guid>
    {
        public Guid UserId { get; set; }

        public Guid? SessionId { get; set; }

        public Guid? MessageId { get; set; }

        public string? Provider { get; set; }

        public string? Model { get; set; }

        public string? ProfileName { get; set; }

        public int InputTokens { get; set; }

        public int OutputTokens { get; set; }

        public int TotalTokens { get; set; }

        public int DurationMs { get; set; }

        public bool IsSuccess { get; set; } = true;

        public string? ErrorMessage { get; set; }
    }
}
