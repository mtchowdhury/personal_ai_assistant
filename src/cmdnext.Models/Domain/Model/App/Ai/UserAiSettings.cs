using System;
using System.ComponentModel.DataAnnotations.Schema;
using CmdNext.Models.Domain.DTOs.Constants;
using CmdNext.Models.Domain.Model.Abstraction;

namespace CmdNext.Models.Domain.Model.App.Ai
{
    [Table("UserAiSettings", Schema = DBSchema.Ai)]
    public class UserAiSettings : BaseEntity<Guid>
    {
        public Guid UserId { get; set; }

        public string? DefaultProvider { get; set; }

        public string? DefaultModel { get; set; }

        public float? Temperature { get; set; }

        public int? MaxOutputTokens { get; set; }

        public string? SystemPromptOverride { get; set; }

        public int? MaxHistoryMessages { get; set; }

        public bool IsChatEnabled { get; set; } = true;
    }
}
