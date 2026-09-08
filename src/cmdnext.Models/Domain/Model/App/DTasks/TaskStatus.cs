using System;
using System.ComponentModel.DataAnnotations.Schema;
using CmdNext.Models.Domain.DTOs.Constants;
using CmdNext.Models.Domain.Model.Abstraction;

namespace CmdNext.Models.Domain.Model.App.DTasks
{
    /// <summary>
    /// A user-defined task status ("To do", "Blocked", ...). Per-user rows, so the
    /// board columns are whatever the user has configured. Tasks reference this by
    /// <see cref="Id"/>, so renaming a status updates its display everywhere.
    /// </summary>
    [Table("TaskStatuses", Schema = DBSchema.DTasks)]
    public class TaskStatus : BaseEntity<Guid>
    {
        public Guid UserId { get; set; }

        public string Name { get; set; } = string.Empty;

        /// <summary>Left-to-right position of this status' column on the kanban board.</summary>
        public int SortOrder { get; set; }

        /// <summary>Hex colour used for the status pill, e.g. "#3f9c6a".</summary>
        public string? Color { get; set; }

        /// <summary>
        /// Marks the terminal status. Tasks in a done status are treated as complete
        /// for progress roll-ups and the "hide completed" filter.
        /// </summary>
        public bool IsDone { get; set; }

        /// <summary>
        /// True for the two statuses the module cannot work without ("To do" and "Done").
        /// Protected rows can be renamed and recoloured but not deleted.
        /// </summary>
        public bool IsProtected { get; set; }
    }
}
