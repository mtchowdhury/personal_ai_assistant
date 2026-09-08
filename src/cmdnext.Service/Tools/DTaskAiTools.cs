using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using CmdNext.Models.Domain.DTOs.DTasks;
using CmdNext.Service.Contracts;

namespace CmdNext.Service.Tools
{
    /// <summary>
    /// AI-callable daily-task tools. Deliberately READ + ADD + COMPLETE only — there is
    /// no delete tool, so the model can never remove a task. Editing is limited to moving
    /// a task's status (including marking it done), which is reversible from the UI.
    /// </summary>
    public class DTaskAiTools : IDTaskAiTools
    {
        private readonly IDTaskService _tasks;
        private readonly ILogger<DTaskAiTools> _logger;
        private readonly Guid _userId;

        public DTaskAiTools(IDTaskService tasks, ILogger<DTaskAiTools> logger, Guid userId)
        {
            _tasks = tasks;
            _logger = logger;
            _userId = userId;
        }

        public async Task<string> AddTaskAsync(
            [Description("Short title of the task, e.g. 'pay the docmorris payment'.")] string title,
            [Description("Optional longer notes / details for the task body.")] string? notes,
            [Description("Scheduled date in ISO format yyyy-MM-dd. Omit for an unscheduled backlog task.")] string? date,
            [Description("Status name — must be one of the user's existing statuses (see the tool description). Defaults to the first open status.")] string? status,
            [Description("Priority: 'Low', 'Medium' or 'High'. Defaults to Medium.")] string? priority,
            [Description("Time of day slot: 'Morning', 'Afternoon', 'Evening' or 'Night'.")] string? timeOfDay,
            [Description("Rough effort estimate in minutes.")] int? approxDurationMinutes,
            [Description("Tag names — should come from the user's existing tags (see the tool description).")] List<string>? tags,
            [Description("Id of the parent task, when adding this as a subtask of an existing task.")] string? parentTaskId)
        {
            Guid? parentId = null;
            if (!string.IsNullOrWhiteSpace(parentTaskId))
            {
                if (!Guid.TryParse(parentTaskId, out var parsedParent))
                    return $"'{parentTaskId}' is not a valid task id.";
                parentId = parsedParent;
            }

            var request = new CreateTaskRequest
            {
                Title = title,
                Notes = notes,
                ScheduledOn = TryDate(date),
                StatusName = status,
                Priority = priority,
                TimeOfDay = timeOfDay,
                ApproxDurationMinutes = approxDurationMinutes,
                Tags = tags ?? new List<string>(),
                ParentId = parentId,
                Source = "ai"
            };

            try
            {
                var created = await _tasks.CreateTaskAsync(_userId, request);
                _logger.LogInformation("AI tool add_task created {TaskId} for user {UserId}", created.Id, _userId);

                var when = created.ScheduledOn.HasValue
                    ? created.ScheduledOn.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                    : "unscheduled";

                return $"Added task '{created.Title}' ({when}, {created.StatusName}, {created.Priority} priority). Task id: {created.Id}.";
            }
            catch (ArgumentException ex) { return $"Could not add the task: {ex.Message}"; }
            catch (InvalidOperationException ex) { return $"Could not add the task: {ex.Message}"; }
        }

        public Task<string> QueryTasksAsync(
            [Description("Free text to match against the task title and notes.")] string? search,
            [Description("Status name filter — one of the user's existing statuses.")] string? status,
            [Description("Priority filter: 'Low', 'Medium' or 'High'.")] string? priority,
            [Description("Tag name filter — one of the user's existing tags.")] string? tag,
            [Description("Start date (inclusive) as yyyy-MM-dd.")] string? fromDate,
            [Description("End date (exclusive) as yyyy-MM-dd.")] string? toDate,
            [Description("Whether to include completed tasks. Defaults to true.")] bool? includeDone)
        {
            return _tasks.QueryTasksAsync(
                _userId, search, status, priority, tag,
                TryDate(fromDate), TryDate(toDate), includeDone ?? true);
        }

        public async Task<string> CompleteTaskAsync(
            [Description("Id of the task to mark as done, as returned by query_tasks.")] string taskId)
        {
            if (!Guid.TryParse(taskId, out var id)) return $"'{taskId}' is not a valid task id.";

            try
            {
                var task = await _tasks.GetTaskAsync(_userId, id);
                if (task == null) return "No task with that id.";
                if (task.IsDone) return $"'{task.Title}' is already marked as {task.StatusName}.";

                var updated = await _tasks.ToggleDoneAsync(_userId, id);
                _logger.LogInformation("AI tool complete_task marked {TaskId} done for user {UserId}", id, _userId);

                return $"Marked '{updated.Title}' as {updated.StatusName}.";
            }
            catch (KeyNotFoundException) { return "No task with that id."; }
            catch (InvalidOperationException ex) { return $"Could not complete the task: {ex.Message}"; }
        }

        private static DateTime? TryDate(string? value)
        {
            if (!string.IsNullOrWhiteSpace(value)
                && DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var d))
            {
                return d;
            }
            return null;
        }

        public async Task<IReadOnlyList<AITool>> GetToolsAsync()
        {
            var statuses = await _tasks.GetStatusesAsync(_userId);
            var tags = await _tasks.GetTagsAsync(_userId);

            var statusNames = statuses.Count > 0
                ? string.Join(", ", statuses.Select(s => s.Name))
                : "To do, Done";
            var tagNames = tags.Count > 0
                ? string.Join(", ", tags.Select(t => t.Name))
                : "(none defined yet)";
            var today = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            var addTaskDescription =
                "Add a daily task for the current user. Use this whenever the user asks to remember, schedule, " +
                "or add something to do. Give the task a short actionable title and put any extra detail in notes. " +
                $"Today is {today}; resolve relative dates like 'tomorrow' or 'next Monday' to an absolute yyyy-MM-dd. " +
                "Leave the date empty only when the user gives no timing at all. " +
                $"Assign one of the user's existing statuses: {statusNames}. " +
                $"Use only the user's existing tags: {tagNames}. Do not invent new statuses or tags. " +
                "To add a subtask, pass the parent's id in parentTaskId (find it with query_tasks first).";

            var queryTasksDescription =
                "Search the current user's daily tasks (read-only). Use this to answer questions like " +
                "'what do I have due today', 'what is still open this week', or to find a task's id before " +
                "completing it. Any argument may be omitted. " +
                $"Today is {today}. The user's statuses are: {statusNames}. Their tags are: {tagNames}.";

            var completeTaskDescription =
                "Mark one of the user's tasks as done. Call query_tasks first to get the task's id, and only " +
                "call this when the user clearly says the task is finished. This cannot delete a task — it only " +
                "moves it into the done status, which the user can undo in the UI.";

            return new List<AITool>
            {
                AIFunctionFactory.Create(AddTaskAsync, "add_task", addTaskDescription),
                AIFunctionFactory.Create(QueryTasksAsync, "query_tasks", queryTasksDescription),
                AIFunctionFactory.Create(CompleteTaskAsync, "complete_task", completeTaskDescription)
            };
        }
    }
}
