using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CmdNext.Models.Domain.DTOs.DTasks;
using CmdNext.Models.Domain.Model.App.DTasks;
using CmdNext.Repository.Contracts;
using CmdNext.Service.Contracts;
// TaskStatus collides with System.Threading.Tasks.TaskStatus.
using DTaskStatus = CmdNext.Models.Domain.Model.App.DTasks.TaskStatus;

namespace CmdNext.Service.Services
{
    public class DTaskService : IDTaskService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<DTaskService> _logger;

        public DTaskService(IUnitOfWork unitOfWork, ILogger<DTaskService> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        private IRepository<DailyTask, Guid> Tasks => _unitOfWork.Repository<DailyTask, Guid>();
        private IRepository<DTaskStatus, Guid> Statuses => _unitOfWork.Repository<DTaskStatus, Guid>();
        private IRepository<TaskTag, Guid> Tags => _unitOfWork.Repository<TaskTag, Guid>();

        // ---- Statuses ----

        public async Task<List<TaskStatusDto>> GetStatusesAsync(Guid userId)
        {
            await EnsureDefaultsAsync(userId);

            var statuses = await Statuses.Query()
                .AsNoTracking()
                .Where(s => s.UserId == userId)
                .OrderBy(s => s.SortOrder).ThenBy(s => s.Name)
                .ToListAsync();

            // One grouped count rather than a query per status.
            var counts = await Tasks.Query()
                .AsNoTracking()
                .Where(t => t.UserId == userId)
                .GroupBy(t => t.StatusId)
                .Select(g => new { StatusId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.StatusId, x => x.Count);

            return statuses.Select(s => ToStatusDto(s, counts.TryGetValue(s.Id, out var c) ? c : 0)).ToList();
        }

        public async Task<TaskStatusDto> CreateStatusAsync(Guid userId, CreateTaskStatusRequest request)
        {
            await EnsureDefaultsAsync(userId);

            var name = request.Name?.Trim();
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Status name is required.");

            var exists = await Statuses.Query()
                .AnyAsync(s => s.UserId == userId && s.Name.ToLower() == name.ToLower());
            if (exists) throw new InvalidOperationException($"A status named '{name}' already exists.");

            var maxOrder = await Statuses.Query()
                .Where(s => s.UserId == userId)
                .Select(s => (int?)s.SortOrder)
                .MaxAsync() ?? -1;

            var status = new DTaskStatus
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Name = name,
                Color = NormalizeColor(request.Color),
                IsDone = request.IsDone,
                IsProtected = false,
                SortOrder = maxOrder + 1,
                CreatedOn = DateTime.UtcNow,
                CreatedBy = userId
            };

            await Statuses.AddAsync(status);
            await _unitOfWork.SaveChangesAsync();
            return ToStatusDto(status, 0);
        }

        public async Task<TaskStatusDto> UpdateStatusAsync(Guid userId, Guid statusId, UpdateTaskStatusRequest request)
        {
            var status = await Statuses.Query()
                .FirstOrDefaultAsync(s => s.Id == statusId && s.UserId == userId)
                ?? throw new KeyNotFoundException("Status not found.");

            if (request.Name != null)
            {
                var name = request.Name.Trim();
                if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Status name is required.");

                var exists = await Statuses.Query()
                    .AnyAsync(s => s.UserId == userId && s.Id != statusId && s.Name.ToLower() == name.ToLower());
                if (exists) throw new InvalidOperationException($"A status named '{name}' already exists.");

                status.Name = name;
            }

            if (request.Color != null) status.Color = NormalizeColor(request.Color);
            if (request.SortOrder is { } order) status.SortOrder = order;

            if (request.IsDone is { } isDone && isDone != status.IsDone)
            {
                // The last done status cannot be un-flagged — toggling and roll-ups depend on one existing.
                if (!isDone)
                {
                    var otherDone = await Statuses.Query()
                        .AnyAsync(s => s.UserId == userId && s.Id != statusId && s.IsDone);
                    if (!otherDone)
                        throw new InvalidOperationException("At least one status must be marked as done.");
                }

                status.IsDone = isDone;

                // Keep CompletedOn consistent with the status' new meaning.
                var affected = await Tasks.Query().Where(t => t.UserId == userId && t.StatusId == statusId).ToListAsync();
                foreach (var task in affected)
                {
                    task.CompletedOn = isDone ? task.CompletedOn ?? DateTime.UtcNow : null;
                    Tasks.Update(task);
                }
            }

            status.UpdatedOn = DateTime.UtcNow;
            status.UpdatedBy = userId;

            Statuses.Update(status);
            await _unitOfWork.SaveChangesAsync();

            var count = await Tasks.Query().CountAsync(t => t.UserId == userId && t.StatusId == statusId);
            return ToStatusDto(status, count);
        }

        public async Task DeleteStatusAsync(Guid userId, Guid statusId)
        {
            var status = await Statuses.Query()
                .FirstOrDefaultAsync(s => s.Id == statusId && s.UserId == userId)
                ?? throw new KeyNotFoundException("Status not found.");

            if (status.IsProtected)
                throw new InvalidOperationException($"'{status.Name}' is a built-in status and cannot be deleted.");

            var inUse = await Tasks.Query().AnyAsync(t => t.StatusId == statusId);
            if (inUse)
                throw new InvalidOperationException(
                    $"'{status.Name}' still has tasks in it. Move them to another status first.");

            Statuses.Delete(status);
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task ReorderStatusesAsync(Guid userId, ReorderStatusesRequest request)
        {
            var statuses = await Statuses.Query().Where(s => s.UserId == userId).ToListAsync();
            var byId = statuses.ToDictionary(s => s.Id);

            var order = 0;
            foreach (var id in request.StatusIds)
            {
                if (!byId.TryGetValue(id, out var status)) continue;
                status.SortOrder = order++;
                status.UpdatedOn = DateTime.UtcNow;
                status.UpdatedBy = userId;
                Statuses.Update(status);
            }

            // Anything the client omitted keeps a stable position after the listed ones.
            foreach (var status in statuses.Where(s => !request.StatusIds.Contains(s.Id)).OrderBy(s => s.SortOrder))
            {
                status.SortOrder = order++;
                Statuses.Update(status);
            }

            await _unitOfWork.SaveChangesAsync();
        }

        // ---- Tags ----

        public async Task<List<TaskTagDto>> GetTagsAsync(Guid userId)
        {
            await EnsureDefaultsAsync(userId);

            var tags = await Tags.Query()
                .AsNoTracking()
                .Where(t => t.UserId == userId)
                .OrderBy(t => t.Name)
                .ToListAsync();

            // Tags live on tasks as a text[], so count by unnesting client-side over the
            // user's tag arrays — cheap next to a per-tag array-contains query each.
            var taskTagArrays = await Tasks.Query()
                .AsNoTracking()
                .Where(t => t.UserId == userId)
                .Select(t => t.Tags)
                .ToListAsync();

            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in taskTagArrays.SelectMany(a => a ?? Array.Empty<string>()))
            {
                counts[name] = counts.TryGetValue(name, out var c) ? c + 1 : 1;
            }

            return tags.Select(t => new TaskTagDto
            {
                Id = t.Id,
                Name = t.Name,
                Color = t.Color,
                TaskCount = counts.TryGetValue(t.Name, out var c) ? c : 0
            }).ToList();
        }

        public async Task<TaskTagDto> CreateTagAsync(Guid userId, CreateTaskTagRequest request)
        {
            await EnsureDefaultsAsync(userId);

            var name = request.Name?.Trim();
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Tag name is required.");

            var exists = await Tags.Query()
                .AnyAsync(t => t.UserId == userId && t.Name.ToLower() == name.ToLower());
            if (exists) throw new InvalidOperationException($"A tag named '{name}' already exists.");

            var tag = new TaskTag
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Name = name,
                Color = NormalizeColor(request.Color),
                CreatedOn = DateTime.UtcNow,
                CreatedBy = userId
            };

            await Tags.AddAsync(tag);
            await _unitOfWork.SaveChangesAsync();
            return new TaskTagDto { Id = tag.Id, Name = tag.Name, Color = tag.Color, TaskCount = 0 };
        }

        public async Task<TaskTagDto> UpdateTagAsync(Guid userId, Guid tagId, UpdateTaskTagRequest request)
        {
            var tag = await Tags.Query()
                .FirstOrDefaultAsync(t => t.Id == tagId && t.UserId == userId)
                ?? throw new KeyNotFoundException("Tag not found.");

            var oldName = tag.Name;

            if (request.Name != null)
            {
                var name = request.Name.Trim();
                if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Tag name is required.");

                var exists = await Tags.Query()
                    .AnyAsync(t => t.UserId == userId && t.Id != tagId && t.Name.ToLower() == name.ToLower());
                if (exists) throw new InvalidOperationException($"A tag named '{name}' already exists.");

                tag.Name = name;
            }

            if (request.Color != null) tag.Color = NormalizeColor(request.Color);

            tag.UpdatedOn = DateTime.UtcNow;
            tag.UpdatedBy = userId;
            Tags.Update(tag);

            // Tags are stored on tasks by name, so a rename has to rewrite the arrays.
            var renamed = 0;
            if (!string.Equals(oldName, tag.Name, StringComparison.Ordinal))
            {
                var affected = await Tasks.Query()
                    .Where(t => t.UserId == userId && t.Tags.Contains(oldName))
                    .ToListAsync();

                foreach (var task in affected)
                {
                    task.Tags = task.Tags
                        .Select(n => string.Equals(n, oldName, StringComparison.OrdinalIgnoreCase) ? tag.Name : n)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                    task.UpdatedOn = DateTime.UtcNow;
                    task.UpdatedBy = userId;
                    Tasks.Update(task);
                }
                renamed = affected.Count;
            }

            await _unitOfWork.SaveChangesAsync();

            if (renamed > 0)
                _logger.LogInformation("Renamed tag '{Old}' to '{New}' on {Count} task(s) for user {UserId}",
                    oldName, tag.Name, renamed, userId);

            var count = await CountTasksWithTagAsync(userId, tag.Name);
            return new TaskTagDto { Id = tag.Id, Name = tag.Name, Color = tag.Color, TaskCount = count };
        }

        public async Task DeleteTagAsync(Guid userId, Guid tagId)
        {
            var tag = await Tags.Query()
                .FirstOrDefaultAsync(t => t.Id == tagId && t.UserId == userId)
                ?? throw new KeyNotFoundException("Tag not found.");

            // Drop the tag from the pick-list and from every task carrying it.
            var affected = await Tasks.Query()
                .Where(t => t.UserId == userId && t.Tags.Contains(tag.Name))
                .ToListAsync();

            foreach (var task in affected)
            {
                task.Tags = task.Tags
                    .Where(n => !string.Equals(n, tag.Name, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                task.UpdatedOn = DateTime.UtcNow;
                task.UpdatedBy = userId;
                Tasks.Update(task);
            }

            Tags.Delete(tag);
            await _unitOfWork.SaveChangesAsync();
        }

        private async Task<int> CountTasksWithTagAsync(Guid userId, string name)
            => await Tasks.Query().CountAsync(t => t.UserId == userId && t.Tags.Contains(name));

        // ---- Tasks ----

        public async Task<List<TaskListItemDto>> GetTasksAsync(
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
            int take = 200)
        {
            await EnsureDefaultsAsync(userId);

            var query = BuildTaskQuery(
                userId, statusIds, tags, priority, category, from, to,
                unscheduled, includeDone, topLevelOnly, search);

            query = ApplySort(query, sortBy, sortDir);

            var tasks = await query.Take(Math.Clamp(take, 1, 1000)).ToListAsync();
            return await ToListItemsAsync(userId, tasks);
        }

        public async Task<List<TaskListItemDto>> GetCalendarTasksAsync(
            Guid userId, int year, int month, bool includeDone = true)
        {
            await EnsureDefaultsAsync(userId);

            // The grid shows leading/trailing days of the adjacent months, so widen the
            // window by a week on each side rather than clipping to the month exactly.
            var monthStart = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
            var from = monthStart.AddDays(-7);
            var to = monthStart.AddMonths(1).AddDays(7);

            var query = Tasks.Query()
                .AsNoTracking()
                .Include(t => t.Status)
                .Where(t => t.UserId == userId
                            && t.ScheduledOn != null
                            && t.ScheduledOn >= from
                            && t.ScheduledOn < to);

            if (!includeDone) query = query.Where(t => !t.Status!.IsDone);

            var tasks = await query
                .OrderBy(t => t.ScheduledOn)
                .ThenBy(t => t.SortOrder)
                .ThenByDescending(t => t.Priority)
                .ToListAsync();

            return await ToListItemsAsync(userId, tasks);
        }

        public async Task<List<TaskBoardColumnDto>> GetBoardAsync(
            Guid userId,
            IReadOnlyCollection<string>? tags = null,
            string? priority = null,
            DateTime? from = null,
            DateTime? to = null,
            bool topLevelOnly = false,
            string? search = null)
        {
            var statuses = await GetStatusesAsync(userId);

            var query = BuildTaskQuery(
                userId, null, tags, priority, null, from, to,
                unscheduled: null, includeDone: true, topLevelOnly: topLevelOnly, search: search);

            var tasks = await query
                .OrderBy(t => t.SortOrder)
                .ThenByDescending(t => t.Priority)
                .ThenBy(t => t.ScheduledOn)
                .ToListAsync();

            var rows = await ToListItemsAsync(userId, tasks);
            var byStatus = rows.GroupBy(r => r.StatusId).ToDictionary(g => g.Key, g => g.ToList());

            // Every status becomes a column, even an empty one — a board with a missing
            // column would make tasks undroppable there.
            return statuses.Select(s => new TaskBoardColumnDto
            {
                StatusId = s.Id,
                StatusName = s.Name,
                StatusColor = s.Color,
                IsDone = s.IsDone,
                SortOrder = s.SortOrder,
                Tasks = byStatus.TryGetValue(s.Id, out var list) ? list : new List<TaskListItemDto>()
            }).ToList();
        }

        public async Task<List<TaskPeriodDto>> GetPeriodsAsync(Guid userId)
        {
            return await Tasks.Query()
                .AsNoTracking()
                .Where(t => t.UserId == userId && t.ScheduledOn != null)
                .GroupBy(t => new { t.ScheduledOn!.Value.Year, t.ScheduledOn!.Value.Month })
                .Select(g => new TaskPeriodDto
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    TaskCount = g.Count()
                })
                .OrderByDescending(p => p.Year).ThenByDescending(p => p.Month)
                .ToListAsync();
        }

        public async Task<TaskDto?> GetTaskAsync(Guid userId, Guid taskId)
        {
            var task = await Tasks.Query()
                .AsNoTracking()
                .Include(t => t.Status)
                .Include(t => t.Parent)
                .FirstOrDefaultAsync(t => t.Id == taskId && t.UserId == userId);

            if (task == null) return null;

            var subtasks = await Tasks.Query()
                .AsNoTracking()
                .Include(t => t.Status)
                .Where(t => t.ParentId == taskId && t.UserId == userId)
                .OrderBy(t => t.SortOrder).ThenBy(t => t.CreatedOn)
                .ToListAsync();

            return ToTaskDto(task, subtasks);
        }

        public async Task<TaskDto> CreateTaskAsync(Guid userId, CreateTaskRequest request)
        {
            await EnsureDefaultsAsync(userId);

            var title = request.Title?.Trim();
            if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Task title is required.");

            var statusId = await ResolveStatusIdAsync(userId, request.StatusId, request.StatusName);
            var status = await Statuses.Query().AsNoTracking().FirstAsync(s => s.Id == statusId);

            Guid? parentId = null;
            if (request.ParentId is { } requestedParent)
            {
                var parent = await Tasks.Query()
                    .FirstOrDefaultAsync(t => t.Id == requestedParent && t.UserId == userId)
                    ?? throw new ArgumentException("Parent task not found.");

                // One level of nesting only: hanging a subtask off a subtask re-parents it
                // to the grandparent rather than building an arbitrarily deep tree.
                parentId = parent.ParentId ?? parent.Id;
            }

            // A task with a parent is a Subtask unless the caller says otherwise.
            var category = ParseCategory(request.Category)
                           ?? (parentId.HasValue ? TaskCategory.Subtask : TaskCategory.Task);

            var maxOrder = await Tasks.Query()
                .Where(t => t.UserId == userId && t.StatusId == statusId)
                .Select(t => (int?)t.SortOrder)
                .MaxAsync() ?? -1;

            var task = new DailyTask
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Title = title,
                Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
                Category = category,
                Priority = ParsePriority(request.Priority) ?? TaskPriority.Medium,
                ScheduledOn = NormalizeDate(request.ScheduledOn),
                TimeOfDay = ParseTimeOfDay(request.TimeOfDay),
                ApproxDurationMinutes = request.ApproxDurationMinutes,
                StatusId = statusId,
                ParentId = parentId,
                Tags = await NormalizeTagsAsync(userId, request.Tags),
                CompletedOn = status.IsDone ? DateTime.UtcNow : null,
                SortOrder = maxOrder + 1,
                Source = string.IsNullOrWhiteSpace(request.Source) ? "manual" : request.Source.Trim().ToLowerInvariant(),
                CreatedOn = DateTime.UtcNow,
                CreatedBy = userId
            };

            await Tasks.AddAsync(task);
            await _unitOfWork.SaveChangesAsync();

            task.Status = status;
            return ToTaskDto(task, new List<DailyTask>());
        }

        public async Task<TaskDto> UpdateTaskAsync(Guid userId, Guid taskId, UpdateTaskRequest request)
        {
            var task = await Tasks.Query()
                .FirstOrDefaultAsync(t => t.Id == taskId && t.UserId == userId)
                ?? throw new KeyNotFoundException("Task not found.");

            if (request.Title != null)
            {
                var title = request.Title.Trim();
                if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Task title is required.");
                task.Title = title;
            }

            if (request.Notes != null)
                task.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();

            if (ParseCategory(request.Category) is { } category) task.Category = category;
            if (ParsePriority(request.Priority) is { } priority) task.Priority = priority;

            if (request.ClearScheduledOn) task.ScheduledOn = null;
            else if (request.ScheduledOn.HasValue) task.ScheduledOn = NormalizeDate(request.ScheduledOn);

            if (request.ClearTimeOfDay) task.TimeOfDay = null;
            else if (ParseTimeOfDay(request.TimeOfDay) is { } timeOfDay) task.TimeOfDay = timeOfDay;

            if (request.ApproxDurationMinutes.HasValue)
                task.ApproxDurationMinutes = request.ApproxDurationMinutes.Value >= 0
                    ? request.ApproxDurationMinutes.Value
                    : null;

            if (request.StatusId is { } newStatusId && newStatusId != task.StatusId)
                await ApplyStatusAsync(userId, task, newStatusId);

            if (request.ClearParent)
            {
                task.ParentId = null;
            }
            else if (request.ParentId is { } newParentId)
            {
                if (newParentId == task.Id) throw new ArgumentException("A task cannot be its own parent.");

                var parent = await Tasks.Query()
                    .FirstOrDefaultAsync(t => t.Id == newParentId && t.UserId == userId)
                    ?? throw new ArgumentException("Parent task not found.");

                // Guard the cycle the one-level rule would otherwise still allow:
                // re-parenting a task under its own child.
                if (parent.ParentId == task.Id)
                    throw new ArgumentException("That task is already a subtask of this one.");

                task.ParentId = parent.ParentId ?? parent.Id;
            }

            if (request.Tags != null) task.Tags = await NormalizeTagsAsync(userId, request.Tags);
            if (request.SortOrder is { } sortOrder) task.SortOrder = sortOrder;

            task.UpdatedOn = DateTime.UtcNow;
            task.UpdatedBy = userId;

            Tasks.Update(task);
            await _unitOfWork.SaveChangesAsync();

            return (await GetTaskAsync(userId, taskId))!;
        }

        public async Task<TaskDto> MoveTaskAsync(Guid userId, Guid taskId, MoveTaskRequest request)
        {
            var task = await Tasks.Query()
                .FirstOrDefaultAsync(t => t.Id == taskId && t.UserId == userId)
                ?? throw new KeyNotFoundException("Task not found.");

            if (request.StatusId is { } statusId && statusId != task.StatusId)
                await ApplyStatusAsync(userId, task, statusId);

            if (request.ClearScheduledOn) task.ScheduledOn = null;
            else if (request.ScheduledOn.HasValue) task.ScheduledOn = NormalizeDate(request.ScheduledOn);

            if (request.SortOrder is { } sortOrder) task.SortOrder = sortOrder;

            task.UpdatedOn = DateTime.UtcNow;
            task.UpdatedBy = userId;

            Tasks.Update(task);
            await _unitOfWork.SaveChangesAsync();

            return (await GetTaskAsync(userId, taskId))!;
        }

        public async Task<TaskDto> ToggleDoneAsync(Guid userId, Guid taskId)
        {
            var task = await Tasks.Query()
                .Include(t => t.Status)
                .FirstOrDefaultAsync(t => t.Id == taskId && t.UserId == userId)
                ?? throw new KeyNotFoundException("Task not found.");

            var statuses = await Statuses.Query()
                .Where(s => s.UserId == userId)
                .OrderBy(s => s.SortOrder)
                .ToListAsync();

            DTaskStatus target;
            if (task.Status!.IsDone)
            {
                // Back out of done into the first open column.
                target = statuses.FirstOrDefault(s => !s.IsDone)
                         ?? throw new InvalidOperationException("No open status is configured.");
            }
            else
            {
                target = statuses.FirstOrDefault(s => s.IsDone)
                         ?? throw new InvalidOperationException("No done status is configured.");
            }

            await ApplyStatusAsync(userId, task, target.Id);

            task.UpdatedOn = DateTime.UtcNow;
            task.UpdatedBy = userId;
            Tasks.Update(task);
            await _unitOfWork.SaveChangesAsync();

            return (await GetTaskAsync(userId, taskId))!;
        }

        public async Task DeleteTaskAsync(Guid userId, Guid taskId)
        {
            var task = await Tasks.Query()
                .FirstOrDefaultAsync(t => t.Id == taskId && t.UserId == userId)
                ?? throw new KeyNotFoundException("Task not found.");

            // Subtasks cascade at the DB level; delete them here too so the change is
            // visible in this unit of work.
            var subtasks = await Tasks.Query().Where(t => t.ParentId == taskId).ToListAsync();
            foreach (var subtask in subtasks) Tasks.Delete(subtask);

            Tasks.Delete(task);
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task<DTaskDashboardDto> GetDashboardAsync(Guid userId)
        {
            await EnsureDefaultsAsync(userId);

            var today = DateTime.UtcNow.Date;
            var tomorrow = today.AddDays(1);

            var open = Tasks.Query()
                .AsNoTracking()
                .Include(t => t.Status)
                .Where(t => t.UserId == userId && !t.Status!.IsDone);

            var overdue = await open
                .Where(t => t.ScheduledOn != null && t.ScheduledOn < today)
                .OrderBy(t => t.ScheduledOn).ThenByDescending(t => t.Priority)
                .Take(20)
                .ToListAsync();

            var todayTasks = await Tasks.Query()
                .AsNoTracking()
                .Include(t => t.Status)
                .Where(t => t.UserId == userId && t.ScheduledOn >= today && t.ScheduledOn < tomorrow)
                .OrderBy(t => t.TimeOfDay).ThenBy(t => t.SortOrder).ThenByDescending(t => t.Priority)
                .ToListAsync();

            return new DTaskDashboardDto
            {
                OverdueCount = await open.CountAsync(t => t.ScheduledOn != null && t.ScheduledOn < today),
                TodayCount = todayTasks.Count,
                TodayDoneCount = todayTasks.Count(t => t.Status!.IsDone),
                UpcomingCount = await open.CountAsync(t => t.ScheduledOn >= tomorrow),
                UnscheduledCount = await open.CountAsync(t => t.ScheduledOn == null),
                Today = await ToListItemsAsync(userId, todayTasks),
                Overdue = await ToListItemsAsync(userId, overdue)
            };
        }

        public async Task<string> QueryTasksAsync(
            Guid userId, string? search, string? status, string? priority,
            string? tag, DateTime? from, DateTime? to, bool includeDone)
        {
            await EnsureDefaultsAsync(userId);

            Guid[]? statusIds = null;
            if (!string.IsNullOrWhiteSpace(status))
            {
                var match = await Statuses.Query()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.UserId == userId && s.Name.ToLower() == status.Trim().ToLower());

                if (match == null)
                {
                    var known = await Statuses.Query().AsNoTracking()
                        .Where(s => s.UserId == userId).Select(s => s.Name).ToListAsync();
                    return $"No status named '{status}'. The user's statuses are: {string.Join(", ", known)}.";
                }
                statusIds = new[] { match.Id };
            }

            var tags = string.IsNullOrWhiteSpace(tag) ? null : new[] { tag.Trim() };

            var query = BuildTaskQuery(
                userId, statusIds, tags, priority, category: null, from: from, to: to,
                unscheduled: null, includeDone: includeDone, topLevelOnly: false, search: search);

            var tasks = await query
                .OrderBy(t => t.ScheduledOn == null)
                .ThenBy(t => t.ScheduledOn)
                .ThenByDescending(t => t.Priority)
                .Take(50)
                .ToListAsync();

            if (tasks.Count == 0) return "No tasks match that query.";

            var sb = new StringBuilder();
            sb.AppendLine($"{tasks.Count} task(s):");
            foreach (var t in tasks)
            {
                var date = t.ScheduledOn.HasValue
                    ? t.ScheduledOn.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                    : "unscheduled";
                var tagText = t.Tags is { Length: > 0 } ? $" [{string.Join(", ", t.Tags)}]" : string.Empty;
                var duration = t.ApproxDurationMinutes.HasValue ? $", ~{t.ApproxDurationMinutes}min" : string.Empty;
                var slot = t.TimeOfDay.HasValue ? $", {t.TimeOfDay}" : string.Empty;
                var parent = t.ParentId.HasValue ? " (subtask)" : string.Empty;

                sb.AppendLine(
                    $"- {t.Title}{parent} — {date}{slot}, {t.Status?.Name ?? "?"}, {t.Priority} priority{duration}{tagText} (id: {t.Id})");
            }

            return sb.ToString().TrimEnd();
        }

        // ---- Helpers ----

        /// <summary>
        /// Builds the shared filtered query used by the list, board and AI-query paths so all
        /// three agree on what a filter means.
        /// </summary>
        private IQueryable<DailyTask> BuildTaskQuery(
            Guid userId,
            IReadOnlyCollection<Guid>? statusIds,
            IReadOnlyCollection<string>? tags,
            string? priority,
            string? category,
            DateTime? from,
            DateTime? to,
            bool? unscheduled,
            bool includeDone,
            bool topLevelOnly,
            string? search)
        {
            var query = Tasks.Query()
                .AsNoTracking()
                .Include(t => t.Status)
                .Where(t => t.UserId == userId);

            if (statusIds is { Count: > 0 }) query = query.Where(t => statusIds.Contains(t.StatusId));
            if (!includeDone) query = query.Where(t => !t.Status!.IsDone);
            if (topLevelOnly) query = query.Where(t => t.ParentId == null);

            if (ParsePriority(priority) is { } p) query = query.Where(t => t.Priority == p);
            if (ParseCategory(category) is { } c) query = query.Where(t => t.Category == c);

            if (unscheduled == true) query = query.Where(t => t.ScheduledOn == null);
            else if (unscheduled == false) query = query.Where(t => t.ScheduledOn != null);

            if (from.HasValue)
            {
                var fromDate = from.Value.Date;
                query = query.Where(t => t.ScheduledOn != null && t.ScheduledOn >= fromDate);
            }
            if (to.HasValue)
            {
                // Exclusive upper bound, so a single-day window is from == to.
                var toDate = to.Value.Date;
                query = query.Where(t => t.ScheduledOn != null && t.ScheduledOn < toDate);
            }

            if (tags is { Count: > 0 })
            {
                // Match any of the requested tags.
                var wanted = tags.Select(t => t.Trim()).Where(t => t.Length > 0).ToArray();
                if (wanted.Length > 0) query = query.Where(t => t.Tags.Any(n => wanted.Contains(n)));
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = $"%{search.Trim()}%";
                query = query.Where(t =>
                    EF.Functions.ILike(t.Title, term) ||
                    (t.Notes != null && EF.Functions.ILike(t.Notes, term)));
            }

            return query;
        }

        private static IQueryable<DailyTask> ApplySort(IQueryable<DailyTask> query, string? sortBy, string? sortDir)
        {
            var desc = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);

            return (sortBy?.Trim().ToLowerInvariant()) switch
            {
                "title" => desc ? query.OrderByDescending(t => t.Title) : query.OrderBy(t => t.Title),
                "priority" => desc ? query.OrderByDescending(t => t.Priority) : query.OrderBy(t => t.Priority),
                "status" => desc
                    ? query.OrderByDescending(t => t.Status!.SortOrder)
                    : query.OrderBy(t => t.Status!.SortOrder),
                "duration" => desc
                    ? query.OrderByDescending(t => t.ApproxDurationMinutes)
                    : query.OrderBy(t => t.ApproxDurationMinutes),
                "createdon" => desc ? query.OrderByDescending(t => t.CreatedOn) : query.OrderBy(t => t.CreatedOn),
                // Default: by date, with unscheduled tasks always last regardless of direction.
                _ => desc
                    ? query.OrderBy(t => t.ScheduledOn == null).ThenByDescending(t => t.ScheduledOn)
                        .ThenBy(t => t.SortOrder)
                    : query.OrderBy(t => t.ScheduledOn == null).ThenBy(t => t.ScheduledOn)
                        .ThenBy(t => t.SortOrder)
            };
        }

        /// <summary>Moves a task to a status and keeps <see cref="DailyTask.CompletedOn"/> in step.</summary>
        private async Task ApplyStatusAsync(Guid userId, DailyTask task, Guid statusId)
        {
            var status = await Statuses.Query()
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == statusId && s.UserId == userId)
                ?? throw new ArgumentException("Status not found.");

            task.StatusId = status.Id;
            task.Status = status;
            task.CompletedOn = status.IsDone ? task.CompletedOn ?? DateTime.UtcNow : null;
        }

        /// <summary>Fills in subtask roll-up counts for a page of tasks in one extra query.</summary>
        private async Task<List<TaskListItemDto>> ToListItemsAsync(Guid userId, List<DailyTask> tasks)
        {
            if (tasks.Count == 0) return new List<TaskListItemDto>();

            var ids = tasks.Select(t => t.Id).ToList();

            var subtaskCounts = await Tasks.Query()
                .AsNoTracking()
                .Include(t => t.Status)
                .Where(t => t.UserId == userId && t.ParentId != null && ids.Contains(t.ParentId!.Value))
                .GroupBy(t => t.ParentId!.Value)
                .Select(g => new
                {
                    ParentId = g.Key,
                    Total = g.Count(),
                    Done = g.Count(t => t.Status!.IsDone)
                })
                .ToListAsync();

            var byParent = subtaskCounts.ToDictionary(x => x.ParentId);

            return tasks.Select(t =>
            {
                byParent.TryGetValue(t.Id, out var counts);
                return new TaskListItemDto
                {
                    Id = t.Id,
                    Title = t.Title,
                    Category = t.Category.ToString(),
                    Priority = t.Priority.ToString(),
                    ScheduledOn = t.ScheduledOn,
                    TimeOfDay = t.TimeOfDay?.ToString(),
                    ApproxDurationMinutes = t.ApproxDurationMinutes,
                    StatusId = t.StatusId,
                    StatusName = t.Status?.Name ?? string.Empty,
                    StatusColor = t.Status?.Color,
                    IsDone = t.Status?.IsDone ?? false,
                    ParentId = t.ParentId,
                    Tags = (t.Tags ?? Array.Empty<string>()).ToList(),
                    HasNotes = !string.IsNullOrWhiteSpace(t.Notes),
                    SubtaskCount = counts?.Total ?? 0,
                    SubtaskDoneCount = counts?.Done ?? 0,
                    Source = t.Source,
                    CompletedOn = t.CompletedOn,
                    SortOrder = t.SortOrder
                };
            }).ToList();
        }

        private static TaskDto ToTaskDto(DailyTask task, List<DailyTask> subtasks) => new()
        {
            Id = task.Id,
            Title = task.Title,
            Notes = task.Notes,
            Category = task.Category.ToString(),
            Priority = task.Priority.ToString(),
            ScheduledOn = task.ScheduledOn,
            TimeOfDay = task.TimeOfDay?.ToString(),
            ApproxDurationMinutes = task.ApproxDurationMinutes,
            StatusId = task.StatusId,
            StatusName = task.Status?.Name ?? string.Empty,
            StatusColor = task.Status?.Color,
            IsDone = task.Status?.IsDone ?? false,
            ParentId = task.ParentId,
            ParentTitle = task.Parent?.Title,
            Tags = (task.Tags ?? Array.Empty<string>()).ToList(),
            CompletedOn = task.CompletedOn,
            SortOrder = task.SortOrder,
            Source = task.Source,
            CreatedOn = task.CreatedOn,
            Subtasks = subtasks.Select(s => new SubtaskDto
            {
                Id = s.Id,
                Title = s.Title,
                StatusId = s.StatusId,
                StatusName = s.Status?.Name ?? string.Empty,
                StatusColor = s.Status?.Color,
                IsDone = s.Status?.IsDone ?? false,
                Priority = s.Priority.ToString(),
                ScheduledOn = s.ScheduledOn,
                SortOrder = s.SortOrder
            }).ToList()
        };

        private static TaskStatusDto ToStatusDto(DTaskStatus s, int taskCount) => new()
        {
            Id = s.Id,
            Name = s.Name,
            SortOrder = s.SortOrder,
            Color = s.Color,
            IsDone = s.IsDone,
            IsProtected = s.IsProtected,
            TaskCount = taskCount
        };

        private async Task<Guid> ResolveStatusIdAsync(Guid userId, Guid? statusId, string? statusName)
        {
            if (statusId is { } id)
            {
                var exists = await Statuses.Query().AnyAsync(s => s.Id == id && s.UserId == userId);
                if (exists) return id;
                throw new ArgumentException("Status not found.");
            }

            if (!string.IsNullOrWhiteSpace(statusName))
            {
                var name = statusName.Trim();
                var match = await Statuses.Query()
                    .FirstOrDefaultAsync(s => s.UserId == userId && s.Name.ToLower() == name.ToLower());
                if (match != null) return match.Id;
            }

            // Fall back to the first open column.
            var fallback = await Statuses.Query()
                .Where(s => s.UserId == userId && !s.IsDone)
                .OrderBy(s => s.SortOrder)
                .FirstOrDefaultAsync();

            if (fallback != null) return fallback.Id;

            throw new InvalidOperationException("No task statuses are configured.");
        }

        /// <summary>
        /// Trims, de-duplicates, and maps tag names onto the user's pick-list casing. Names
        /// that aren't in the pick-list are kept as typed rather than dropped.
        /// </summary>
        private async Task<string[]> NormalizeTagsAsync(Guid userId, List<string>? tags)
        {
            if (tags == null || tags.Count == 0) return Array.Empty<string>();

            var known = await Tags.Query()
                .AsNoTracking()
                .Where(t => t.UserId == userId)
                .Select(t => t.Name)
                .ToListAsync();

            var canonical = known.ToDictionary(n => n, n => n, StringComparer.OrdinalIgnoreCase);

            return tags
                .Select(t => t?.Trim() ?? string.Empty)
                .Where(t => t.Length > 0)
                .Select(t => canonical.TryGetValue(t, out var name) ? name : t)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        /// <summary>
        /// Dates are stored as midnight UTC: the scheduled day is a calendar date, so a
        /// client's local time-of-day must not shift it across a day boundary.
        /// </summary>
        private static DateTime? NormalizeDate(DateTime? value)
            => value.HasValue ? DateTime.SpecifyKind(value.Value.Date, DateTimeKind.Utc) : null;

        private static string? NormalizeColor(string? color)
        {
            var value = color?.Trim();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        private static TaskCategory? ParseCategory(string? value)
            => Enum.TryParse<TaskCategory>(value?.Trim(), ignoreCase: true, out var parsed) ? parsed : null;

        private static TaskPriority? ParsePriority(string? value)
            => Enum.TryParse<TaskPriority>(value?.Trim(), ignoreCase: true, out var parsed) ? parsed : null;

        private static TaskTimeOfDay? ParseTimeOfDay(string? value)
            => Enum.TryParse<TaskTimeOfDay>(value?.Trim(), ignoreCase: true, out var parsed) ? parsed : null;

        /// <summary>Seeds the starter statuses and tags the first time a user touches the module.</summary>
        private async Task EnsureDefaultsAsync(Guid userId)
        {
            var now = DateTime.UtcNow;
            var changed = false;

            if (!await Statuses.Query().AnyAsync(s => s.UserId == userId))
            {
                foreach (var (name, sortOrder, color, isDone, isProtected) in DefaultTaskStatuses.All)
                {
                    await Statuses.AddAsync(new DTaskStatus
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId,
                        Name = name,
                        SortOrder = sortOrder,
                        Color = color,
                        IsDone = isDone,
                        IsProtected = isProtected,
                        CreatedOn = now,
                        CreatedBy = userId
                    });
                }
                changed = true;
            }

            if (!await Tags.Query().AnyAsync(t => t.UserId == userId))
            {
                foreach (var (name, color) in DefaultTaskTags.All)
                {
                    await Tags.AddAsync(new TaskTag
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId,
                        Name = name,
                        Color = color,
                        CreatedOn = now,
                        CreatedBy = userId
                    });
                }
                changed = true;
            }

            if (changed) await _unitOfWork.SaveChangesAsync();
        }
    }
}
