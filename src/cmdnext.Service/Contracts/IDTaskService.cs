using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CmdNext.Models.Domain.DTOs.DTasks;

namespace CmdNext.Service.Contracts
{
    public interface IDTaskService
    {
        // ---- Statuses ----
        Task<List<TaskStatusDto>> GetStatusesAsync(Guid userId);
        Task<TaskStatusDto> CreateStatusAsync(Guid userId, CreateTaskStatusRequest request);
        Task<TaskStatusDto> UpdateStatusAsync(Guid userId, Guid statusId, UpdateTaskStatusRequest request);
        Task DeleteStatusAsync(Guid userId, Guid statusId);
        Task ReorderStatusesAsync(Guid userId, ReorderStatusesRequest request);

        // ---- Tags ----
        Task<List<TaskTagDto>> GetTagsAsync(Guid userId);
        Task<TaskTagDto> CreateTagAsync(Guid userId, CreateTaskTagRequest request);
        Task<TaskTagDto> UpdateTagAsync(Guid userId, Guid tagId, UpdateTaskTagRequest request);
        Task DeleteTagAsync(Guid userId, Guid tagId);

        // ---- Tasks ----
        /// <summary>
        /// The list view. Every filter is optional and ANDed together; <paramref name="statusIds"/>
        /// and <paramref name="tags"/> match any of the given values.
        /// </summary>
        Task<List<TaskListItemDto>> GetTasksAsync(
            Guid userId,
            IReadOnlyCollection<Guid>? statusIds = null,
            IReadOnlyCollection<string>? tags = null,
            string? priority = null,
            string? category = null,
            DateTime? from = null,
            DateTime? to = null,
            bool? unscheduled = null,
            bool includeDone = true,
            bool topLevelOnly = false,
            string? search = null,
            string sortBy = "scheduledOn",
            string sortDir = "asc",
            int take = 200);

        /// <summary>Tasks scheduled within a month, for the calendar grid.</summary>
        Task<List<TaskListItemDto>> GetCalendarTasksAsync(Guid userId, int year, int month, bool includeDone = true);

        /// <summary>The kanban board: one column per status, tasks bucketed into them.</summary>
        Task<List<TaskBoardColumnDto>> GetBoardAsync(
            Guid userId,
            IReadOnlyCollection<string>? tags = null,
            string? priority = null,
            DateTime? from = null,
            DateTime? to = null,
            bool topLevelOnly = false,
            string? search = null);

        /// <summary>Months that actually contain scheduled tasks, newest first.</summary>
        Task<List<TaskPeriodDto>> GetPeriodsAsync(Guid userId);

        Task<TaskDto?> GetTaskAsync(Guid userId, Guid taskId);
        Task<TaskDto> CreateTaskAsync(Guid userId, CreateTaskRequest request);
        Task<TaskDto> UpdateTaskAsync(Guid userId, Guid taskId, UpdateTaskRequest request);
        /// <summary>Drag-and-drop: change status and/or scheduled date in one call.</summary>
        Task<TaskDto> MoveTaskAsync(Guid userId, Guid taskId, MoveTaskRequest request);
        /// <summary>Flips the task between its current status and the user's done status.</summary>
        Task<TaskDto> ToggleDoneAsync(Guid userId, Guid taskId);
        Task DeleteTaskAsync(Guid userId, Guid taskId);

        Task<DTaskDashboardDto> GetDashboardAsync(Guid userId);

        /// <summary>Read-only query used by the AI tool and by ad-hoc questions.</summary>
        Task<string> QueryTasksAsync(
            Guid userId, string? search, string? status, string? priority,
            string? tag, DateTime? from, DateTime? to, bool includeDone);
    }
}
