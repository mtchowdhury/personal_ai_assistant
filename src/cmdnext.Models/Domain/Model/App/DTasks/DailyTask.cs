using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using CmdNext.Models.Domain.DTOs.Constants;
using CmdNext.Models.Domain.Model.Abstraction;

namespace CmdNext.Models.Domain.Model.App.DTasks
{
    /// <summary>Whether a row is a top-level task or a child of another task.</summary>
    public enum TaskCategory
    {
        Task = 0,
        Subtask = 1
    }

    public enum TaskPriority
    {
        Low = 0,
        Medium = 1,
        High = 2
    }

    /// <summary>Coarse slot within the day, for ordering the day's work without a real clock time.</summary>
    public enum TaskTimeOfDay
    {
        Morning = 0,
        Afternoon = 1,
        Evening = 2,
        Night = 3
    }

    /// <summary>
    /// A single daily task. <see cref="ScheduledOn"/> is the date the task sits on in the
    /// calendar view — the equivalent of the Notion "Date" property — and is deliberately a
    /// date, not a timestamp; time-of-day granularity is the <see cref="TimeOfDay"/> slot.
    /// </summary>
    [Table("Tasks", Schema = DBSchema.DTasks)]
    public class DailyTask : BaseEntity<Guid>
    {
        public Guid UserId { get; set; }

        public string Title { get; set; } = string.Empty;

        /// <summary>Free markdown body — the Notion page content behind a task card.</summary>
        public string? Notes { get; set; }

        public TaskCategory Category { get; set; } = TaskCategory.Task;

        public TaskPriority Priority { get; set; } = TaskPriority.Medium;

        /// <summary>The date this task is scheduled for. Null means unscheduled (backlog).</summary>
        public DateTime? ScheduledOn { get; set; }

        public TaskTimeOfDay? TimeOfDay { get; set; }

        /// <summary>Estimated effort in minutes ("Approx duration" in Notion).</summary>
        public int? ApproxDurationMinutes { get; set; }

        public Guid StatusId { get; set; }

        public TaskStatus? Status { get; set; }

        /// <summary>Parent task for a subtask; null for a top-level task.</summary>
        public Guid? ParentId { get; set; }

        public DailyTask? Parent { get; set; }

        public List<DailyTask> Subtasks { get; set; } = new();

        /// <summary>
        /// Tag names, denormalised as a native text[]. Values are drawn from the user's
        /// <see cref="TaskTag"/> pick-list in the UI but are not FK-enforced.
        /// </summary>
        public string[] Tags { get; set; } = Array.Empty<string>();

        /// <summary>Set when the task moves into a status with IsDone; cleared when it moves back out.</summary>
        public DateTime? CompletedOn { get; set; }

        /// <summary>Manual ordering within a kanban column / day cell.</summary>
        public int SortOrder { get; set; }

        /// <summary>"ai" or "manual" — how the record was created.</summary>
        public string Source { get; set; } = "manual";
    }
}
