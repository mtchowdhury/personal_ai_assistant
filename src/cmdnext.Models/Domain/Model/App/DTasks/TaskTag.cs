using System;
using System.ComponentModel.DataAnnotations.Schema;
using CmdNext.Models.Domain.DTOs.Constants;
using CmdNext.Models.Domain.Model.Abstraction;

namespace CmdNext.Models.Domain.Model.App.DTasks
{
    /// <summary>
    /// The per-user vocabulary of tags offered in the tag dropdown. The tags actually
    /// applied to a task live in <see cref="DailyTask.Tags"/> as a text[] of names —
    /// this table only supplies the pick-list, so removing a tag here does not rewrite
    /// history on existing tasks.
    /// </summary>
    [Table("TaskTags", Schema = DBSchema.DTasks)]
    public class TaskTag : BaseEntity<Guid>
    {
        public Guid UserId { get; set; }

        public string Name { get; set; } = string.Empty;

        /// <summary>Hex colour used for the tag chip.</summary>
        public string? Color { get; set; }
    }
}
