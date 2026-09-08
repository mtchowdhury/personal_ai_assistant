using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CmdNext.Api.Infrastructure;
using CmdNext.Models.Domain.DTOs.DTasks;
using CmdNext.Service.Contracts;

namespace CmdNext.Api.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Authorize]
    [Route("api/v{version:apiVersion}/[controller]")]
    public class DTasksController : ApiControllerBase
    {
        private readonly IDTaskService _tasks;

        public DTasksController(IDTaskService tasks)
        {
            _tasks = tasks;
        }

        // ---- Statuses ----

        [HttpGet("statuses")]
        public async Task<ActionResult> GetStatuses() => Ok(await _tasks.GetStatusesAsync(CurrentUserId));

        [HttpPost("statuses")]
        public async Task<ActionResult> CreateStatus([FromBody] CreateTaskStatusRequest request)
        {
            try { return Ok(await _tasks.CreateStatusAsync(CurrentUserId, request)); }
            catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
            catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        }

        [HttpPut("statuses/{id:guid}")]
        public async Task<ActionResult> UpdateStatus(Guid id, [FromBody] UpdateTaskStatusRequest request)
        {
            try { return Ok(await _tasks.UpdateStatusAsync(CurrentUserId, id, request)); }
            catch (KeyNotFoundException) { return NotFound(new { message = "Status not found" }); }
            catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
            catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        }

        [HttpDelete("statuses/{id:guid}")]
        public async Task<ActionResult> DeleteStatus(Guid id)
        {
            try { await _tasks.DeleteStatusAsync(CurrentUserId, id); return Ok(new { message = "Deleted" }); }
            catch (KeyNotFoundException) { return NotFound(new { message = "Status not found" }); }
            catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        }

        /// <summary>Reorders the kanban columns.</summary>
        [HttpPut("statuses/reorder")]
        public async Task<ActionResult> ReorderStatuses([FromBody] ReorderStatusesRequest request)
        {
            await _tasks.ReorderStatusesAsync(CurrentUserId, request);
            return Ok(new { message = "Reordered" });
        }

        // ---- Tags ----

        [HttpGet("tags")]
        public async Task<ActionResult> GetTags() => Ok(await _tasks.GetTagsAsync(CurrentUserId));

        [HttpPost("tags")]
        public async Task<ActionResult> CreateTag([FromBody] CreateTaskTagRequest request)
        {
            try { return Ok(await _tasks.CreateTagAsync(CurrentUserId, request)); }
            catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
            catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        }

        [HttpPut("tags/{id:guid}")]
        public async Task<ActionResult> UpdateTag(Guid id, [FromBody] UpdateTaskTagRequest request)
        {
            try { return Ok(await _tasks.UpdateTagAsync(CurrentUserId, id, request)); }
            catch (KeyNotFoundException) { return NotFound(new { message = "Tag not found" }); }
            catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
            catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        }

        [HttpDelete("tags/{id:guid}")]
        public async Task<ActionResult> DeleteTag(Guid id)
        {
            try { await _tasks.DeleteTagAsync(CurrentUserId, id); return Ok(new { message = "Deleted" }); }
            catch (KeyNotFoundException) { return NotFound(new { message = "Tag not found" }); }
        }

        // ---- Tasks ----

        /// <summary>
        /// The list view. Repeat <c>statusId</c> or <c>tag</c> to match several at once;
        /// omit them for all.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult> GetTasks(
            [FromQuery(Name = "statusId")] Guid[]? statusId,
            [FromQuery(Name = "tag")] string[]? tag,
            [FromQuery] string? priority = null,
            [FromQuery] string? category = null,
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null,
            [FromQuery] bool? unscheduled = null,
            [FromQuery] bool includeDone = true,
            [FromQuery] bool topLevelOnly = false,
            [FromQuery] string? search = null,
            [FromQuery] string sortBy = "scheduledOn",
            [FromQuery] string sortDir = "asc",
            [FromQuery] int take = 200)
            => Ok(await _tasks.GetTasksAsync(
                CurrentUserId, statusId, tag, priority, category, from, to,
                unscheduled, includeDone, topLevelOnly, search, sortBy, sortDir, take));

        /// <summary>Tasks for one month, for the calendar grid.</summary>
        [HttpGet("calendar")]
        public async Task<ActionResult> GetCalendar(
            [FromQuery] int year,
            [FromQuery] int month,
            [FromQuery] bool includeDone = true)
        {
            if (month < 1 || month > 12) return BadRequest(new { message = "Month must be between 1 and 12." });
            if (year < 1) return BadRequest(new { message = "Year is required." });
            return Ok(await _tasks.GetCalendarTasksAsync(CurrentUserId, year, month, includeDone));
        }

        /// <summary>The kanban board: one column per status.</summary>
        [HttpGet("board")]
        public async Task<ActionResult> GetBoard(
            [FromQuery(Name = "tag")] string[]? tag,
            [FromQuery] string? priority = null,
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null,
            [FromQuery] bool topLevelOnly = false,
            [FromQuery] string? search = null)
            => Ok(await _tasks.GetBoardAsync(CurrentUserId, tag, priority, from, to, topLevelOnly, search));

        /// <summary>Months that actually contain scheduled tasks, newest first.</summary>
        [HttpGet("periods")]
        public async Task<ActionResult> GetPeriods() => Ok(await _tasks.GetPeriodsAsync(CurrentUserId));

        [HttpGet("dashboard")]
        public async Task<ActionResult> GetDashboard() => Ok(await _tasks.GetDashboardAsync(CurrentUserId));

        [HttpGet("{id:guid}")]
        public async Task<ActionResult> GetTask(Guid id)
        {
            var task = await _tasks.GetTaskAsync(CurrentUserId, id);
            return task == null ? NotFound(new { message = "Task not found" }) : Ok(task);
        }

        [HttpPost]
        public async Task<ActionResult> CreateTask([FromBody] CreateTaskRequest request)
        {
            // Manual entry from the UI is always source "manual".
            request.Source = "manual";
            try { return Ok(await _tasks.CreateTaskAsync(CurrentUserId, request)); }
            catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
            catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult> UpdateTask(Guid id, [FromBody] UpdateTaskRequest request)
        {
            try { return Ok(await _tasks.UpdateTaskAsync(CurrentUserId, id, request)); }
            catch (KeyNotFoundException) { return NotFound(new { message = "Task not found" }); }
            catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        }

        /// <summary>Drag-and-drop: change status and/or scheduled date.</summary>
        [HttpPut("{id:guid}/move")]
        public async Task<ActionResult> MoveTask(Guid id, [FromBody] MoveTaskRequest request)
        {
            try { return Ok(await _tasks.MoveTaskAsync(CurrentUserId, id, request)); }
            catch (KeyNotFoundException) { return NotFound(new { message = "Task not found" }); }
            catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        }

        /// <summary>Flips a task between done and the first open status.</summary>
        [HttpPut("{id:guid}/toggle")]
        public async Task<ActionResult> ToggleDone(Guid id)
        {
            try { return Ok(await _tasks.ToggleDoneAsync(CurrentUserId, id)); }
            catch (KeyNotFoundException) { return NotFound(new { message = "Task not found" }); }
            catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        }

        [HttpDelete("{id:guid}")]
        public async Task<ActionResult> DeleteTask(Guid id)
        {
            try { await _tasks.DeleteTaskAsync(CurrentUserId, id); return Ok(new { message = "Deleted" }); }
            catch (KeyNotFoundException) { return NotFound(new { message = "Task not found" }); }
        }
    }
}
