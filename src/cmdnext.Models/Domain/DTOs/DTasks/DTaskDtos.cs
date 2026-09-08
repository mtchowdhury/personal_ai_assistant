using System;
using System.Collections.Generic;

namespace CmdNext.Models.Domain.DTOs.DTasks
{
    /// <summary>The starter set of statuses, seeded per-user on first use.</summary>
    public static class DefaultTaskStatuses
    {
        public static readonly (string Name, int SortOrder, string Color, bool IsDone, bool IsProtected)[] All =
        {
            ("To do",       0, "#6b7280", false, true),
            ("In Progress", 1, "#2f6fed", false, false),
            ("On Hold",     2, "#c98a2b", false, false),
            ("Blocked",     3, "#c0453b", false, false),
            ("Done",        4, "#3f9c6a", true,  true)
        };
    }

    /// <summary>The starter tag vocabulary, seeded per-user on first use.</summary>
    public static class DefaultTaskTags
    {
        public static readonly (string Name, string Color)[] All =
        {
            ("Households", "#8b5cf6"),
            ("Personal",   "#2f6fed"),
            ("Learning",   "#0d9488"),
            ("Health",     "#c0453b"),
            ("Finance",    "#c98a2b")
        };
    }

    public class TaskStatusDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public string? Color { get; set; }
        public bool IsDone { get; set; }
        public bool IsProtected { get; set; }
        /// <summary>How many of the user's tasks currently sit in this status.</summary>
        public int TaskCount { get; set; }
    }

    public class CreateTaskStatusRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Color { get; set; }
        public bool IsDone { get; set; }
    }

    public class UpdateTaskStatusRequest
    {
        public string? Name { get; set; }
        public string? Color { get; set; }
        public bool? IsDone { get; set; }
        public int? SortOrder { get; set; }
    }

    /// <summary>Reorders the kanban columns in one call.</summary>
    public class ReorderStatusesRequest
    {
        public List<Guid> StatusIds { get; set; } = new();
    }

    public class TaskTagDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Color { get; set; }
        public int TaskCount { get; set; }
    }

    public class CreateTaskTagRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Color { get; set; }
    }

    public class UpdateTaskTagRequest
    {
        public string? Name { get; set; }
        public string? Color { get; set; }
    }

    /// <summary>A subtask as shown nested under its parent.</summary>
    public class SubtaskDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public Guid StatusId { get; set; }
        public string StatusName { get; set; } = string.Empty;
        public string? StatusColor { get; set; }
        public bool IsDone { get; set; }
        public string Priority { get; set; } = "Medium";
        public DateTime? ScheduledOn { get; set; }
        public int SortOrder { get; set; }
    }

    public class TaskDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public string Category { get; set; } = "Task";
        public string Priority { get; set; } = "Medium";
        public DateTime? ScheduledOn { get; set; }
        public string? TimeOfDay { get; set; }
        public int? ApproxDurationMinutes { get; set; }
        public Guid StatusId { get; set; }
        public string StatusName { get; set; } = string.Empty;
        public string? StatusColor { get; set; }
        public bool IsDone { get; set; }
        public Guid? ParentId { get; set; }
        public string? ParentTitle { get; set; }
        public List<string> Tags { get; set; } = new();
        public DateTime? CompletedOn { get; set; }
        public int SortOrder { get; set; }
        public string Source { get; set; } = "manual";
        public DateTime? CreatedOn { get; set; }
        public List<SubtaskDto> Subtasks { get; set; } = new();
    }

    /// <summary>Row shape for the list / kanban / calendar views — no notes, no nested subtasks.</summary>
    public class TaskListItemDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Category { get; set; } = "Task";
        public string Priority { get; set; } = "Medium";
        public DateTime? ScheduledOn { get; set; }
        public string? TimeOfDay { get; set; }
        public int? ApproxDurationMinutes { get; set; }
        public Guid StatusId { get; set; }
        public string StatusName { get; set; } = string.Empty;
        public string? StatusColor { get; set; }
        public bool IsDone { get; set; }
        public Guid? ParentId { get; set; }
        public List<string> Tags { get; set; } = new();
        public bool HasNotes { get; set; }
        public int SubtaskCount { get; set; }
        public int SubtaskDoneCount { get; set; }
        public string Source { get; set; } = "manual";
        public DateTime? CompletedOn { get; set; }
        public int SortOrder { get; set; }
    }

    public class CreateTaskRequest
    {
        public string Title { get; set; } = string.Empty;
        public string? Notes { get; set; }
        /// <summary>"Task" or "Subtask". Defaults to Subtask when a parent is given, else Task.</summary>
        public string? Category { get; set; }
        /// <summary>"Low", "Medium" or "High". Defaults to Medium.</summary>
        public string? Priority { get; set; }
        public DateTime? ScheduledOn { get; set; }
        /// <summary>"Morning", "Afternoon", "Evening" or "Night".</summary>
        public string? TimeOfDay { get; set; }
        public int? ApproxDurationMinutes { get; set; }
        public Guid? StatusId { get; set; }
        /// <summary>Status name as a convenience for the AI tool; resolved to an id server-side.</summary>
        public string? StatusName { get; set; }
        public Guid? ParentId { get; set; }
        public List<string> Tags { get; set; } = new();
        /// <summary>"ai" or "manual"; defaults to manual.</summary>
        public string? Source { get; set; }
    }

    public class UpdateTaskRequest
    {
        public string? Title { get; set; }
        public string? Notes { get; set; }
        public string? Category { get; set; }
        public string? Priority { get; set; }
        public DateTime? ScheduledOn { get; set; }
        /// <summary>True to clear the date (send the task back to the backlog).</summary>
        public bool ClearScheduledOn { get; set; }
        public string? TimeOfDay { get; set; }
        public bool ClearTimeOfDay { get; set; }
        public int? ApproxDurationMinutes { get; set; }
        public Guid? StatusId { get; set; }
        public Guid? ParentId { get; set; }
        public bool ClearParent { get; set; }
        /// <summary>When provided, fully replaces the task's tags. Null leaves them unchanged.</summary>
        public List<string>? Tags { get; set; }
        public int? SortOrder { get; set; }
    }

    /// <summary>Drag-and-drop move on the board or calendar.</summary>
    public class MoveTaskRequest
    {
        public Guid? StatusId { get; set; }
        public DateTime? ScheduledOn { get; set; }
        public bool ClearScheduledOn { get; set; }
        public int? SortOrder { get; set; }
    }

    /// <summary>One kanban column with the tasks currently in it.</summary>
    public class TaskBoardColumnDto
    {
        public Guid StatusId { get; set; }
        public string StatusName { get; set; } = string.Empty;
        public string? StatusColor { get; set; }
        public bool IsDone { get; set; }
        public int SortOrder { get; set; }
        public List<TaskListItemDto> Tasks { get; set; } = new();
    }

    /// <summary>A month/year that has at least one scheduled task.</summary>
    public class TaskPeriodDto
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public int TaskCount { get; set; }
    }

    public class DTaskDashboardDto
    {
        public int OverdueCount { get; set; }
        public int TodayCount { get; set; }
        public int TodayDoneCount { get; set; }
        public int UpcomingCount { get; set; }
        public int UnscheduledCount { get; set; }
        public List<TaskListItemDto> Today { get; set; } = new();
        public List<TaskListItemDto> Overdue { get; set; } = new();
    }
}
