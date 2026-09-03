using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CmdNext.Api.Infrastructure;
using CmdNext.Models.Domain.DTOs.Spaces;
using CmdNext.Service.Contracts;

namespace CmdNext.Api.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Authorize]
    [Route("api/v{version:apiVersion}/[controller]")]
    public class SpacesController : ApiControllerBase
    {
        private const long MaxAttachmentBytes = 25 * 1024 * 1024;

        private readonly ISpaceService _spaces;

        public SpacesController(ISpaceService spaces)
        {
            _spaces = spaces;
        }

        // ---- Templates ----

        [HttpGet("templates")]
        public ActionResult<List<SpaceTemplateDto>> GetTemplates() => Ok(_spaces.GetTemplates());

        // ---- Spaces ----

        [HttpGet]
        public async Task<ActionResult> GetSpaces([FromQuery] bool includeArchived = false) =>
            Ok(await _spaces.GetSpacesAsync(CurrentUserId, includeArchived));

        [HttpGet("{id:guid}")]
        public async Task<ActionResult> GetSpace(Guid id)
        {
            try { return Ok(await _spaces.GetSpaceAsync(CurrentUserId, id)); }
            catch (KeyNotFoundException) { return NotFound(new { message = "Space not found" }); }
        }

        [HttpPost]
        public async Task<ActionResult> CreateSpace([FromBody] CreateSpaceRequest request)
        {
            try { return Ok(await _spaces.CreateSpaceAsync(CurrentUserId, request)); }
            catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult> UpdateSpace(Guid id, [FromBody] UpdateSpaceRequest request)
        {
            try { return Ok(await _spaces.UpdateSpaceAsync(CurrentUserId, id, request)); }
            catch (KeyNotFoundException) { return NotFound(new { message = "Space not found" }); }
            catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HttpPut("{id:guid}/state")]
        public async Task<ActionResult> UpdateState(Guid id, [FromBody] UpdateSpaceStateRequest request)
        {
            try { return Ok(await _spaces.UpdateSpaceStateAsync(CurrentUserId, id, request)); }
            catch (KeyNotFoundException) { return NotFound(new { message = "Space not found" }); }
        }

        [HttpPut("{id:guid}/schema")]
        public async Task<ActionResult> UpdateSchema(Guid id, [FromBody] UpdateSpaceSchemaRequest request)
        {
            try { return Ok(await _spaces.UpdateSpaceSchemaAsync(CurrentUserId, id, request)); }
            catch (KeyNotFoundException) { return NotFound(new { message = "Space not found" }); }
            catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HttpDelete("{id:guid}")]
        public async Task<ActionResult> DeleteSpace(Guid id, [FromQuery] bool hard = false)
        {
            try
            {
                if (hard) await _spaces.DeleteSpaceAsync(CurrentUserId, id);
                else await _spaces.ArchiveSpaceAsync(CurrentUserId, id);
                return Ok(new { message = hard ? "Deleted" : "Archived" });
            }
            catch (KeyNotFoundException) { return NotFound(new { message = "Space not found" }); }
        }

        // ---- Nodes ----

        [HttpGet("{id:guid}/nodes")]
        public async Task<ActionResult> GetNodes(Guid id)
        {
            try { return Ok(await _spaces.GetNodesAsync(CurrentUserId, id)); }
            catch (KeyNotFoundException) { return NotFound(new { message = "Space not found" }); }
        }

        [HttpPost("{id:guid}/nodes")]
        public async Task<ActionResult> CreateNode(Guid id, [FromBody] CreateNodeRequest request)
        {
            try { return Ok(await _spaces.CreateNodeAsync(CurrentUserId, id, request)); }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HttpPut("{id:guid}/nodes/{nodeId:guid}")]
        public async Task<ActionResult> UpdateNode(Guid id, Guid nodeId, [FromBody] UpdateNodeRequest request)
        {
            try { return Ok(await _spaces.UpdateNodeAsync(CurrentUserId, id, nodeId, request)); }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HttpDelete("{id:guid}/nodes/{nodeId:guid}")]
        public async Task<ActionResult> DeleteNode(Guid id, Guid nodeId)
        {
            try { await _spaces.DeleteNodeAsync(CurrentUserId, id, nodeId); return Ok(new { message = "Deleted" }); }
            catch (KeyNotFoundException) { return NotFound(new { message = "Node not found" }); }
            catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        }

        // ---- Entries ----

        [HttpGet("{id:guid}/entries")]
        public async Task<ActionResult> GetEntries(Guid id, [FromQuery] EntryQuery query)
        {
            try { return Ok(await _spaces.GetEntriesAsync(CurrentUserId, id, query)); }
            catch (KeyNotFoundException) { return NotFound(new { message = "Space not found" }); }
        }

        [HttpGet("{id:guid}/entries/{entryId:guid}")]
        public async Task<ActionResult> GetEntry(Guid id, Guid entryId)
        {
            try { return Ok(await _spaces.GetEntryAsync(CurrentUserId, id, entryId)); }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        }

        [HttpPost("{id:guid}/entries")]
        public async Task<ActionResult> CreateEntry(Guid id, [FromBody] CreateEntryRequest request)
        {
            try { return Ok(await _spaces.CreateEntryAsync(CurrentUserId, id, request)); }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HttpPut("{id:guid}/entries/{entryId:guid}")]
        public async Task<ActionResult> UpdateEntry(Guid id, Guid entryId, [FromBody] UpdateEntryRequest request)
        {
            try { return Ok(await _spaces.UpdateEntryAsync(CurrentUserId, id, entryId, request)); }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HttpPost("{id:guid}/entries/{entryId:guid}/append")]
        public async Task<ActionResult> AppendToEntry(Guid id, Guid entryId, [FromBody] AppendToEntryRequest request)
        {
            try { return Ok(await _spaces.AppendToEntryAsync(CurrentUserId, id, entryId, request)); }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HttpDelete("{id:guid}/entries/{entryId:guid}")]
        public async Task<ActionResult> DeleteEntry(Guid id, Guid entryId)
        {
            try { await _spaces.DeleteEntryAsync(CurrentUserId, id, entryId); return Ok(new { message = "Deleted" }); }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        }

        // ---- Search ----

        [HttpPost("search")]
        public async Task<ActionResult> Search([FromBody] SearchEntriesRequest request) =>
            Ok(await _spaces.SearchAsync(CurrentUserId, request));

        // ---- Attachments ----

        [HttpPost("{id:guid}/attachments")]
        [RequestSizeLimit(MaxAttachmentBytes)]
        public async Task<ActionResult> UploadAttachment(Guid id, IFormFile file, [FromQuery] Guid? nodeId, [FromQuery] Guid? entryId)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "No file uploaded" });
            if (file.Length > MaxAttachmentBytes)
                return BadRequest(new { message = "File too large (max 25 MB)" });

            try
            {
                await using var stream = file.OpenReadStream();
                var attachment = await _spaces.AddAttachmentAsync(
                    CurrentUserId, id, nodeId, entryId, file.FileName, file.ContentType, stream, extractedText: null);
                return Ok(attachment);
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        }

        [HttpGet("{id:guid}/attachments")]
        public async Task<ActionResult> GetAttachments(Guid id, [FromQuery] Guid? nodeId, [FromQuery] Guid? entryId)
        {
            try { return Ok(await _spaces.GetAttachmentsAsync(CurrentUserId, id, nodeId, entryId)); }
            catch (KeyNotFoundException) { return NotFound(new { message = "Space not found" }); }
        }

        [HttpGet("{id:guid}/attachments/{attachmentId:guid}")]
        public async Task<ActionResult> GetAttachment(Guid id, Guid attachmentId)
        {
            try
            {
                var (stream, contentType, fileName) = await _spaces.OpenAttachmentAsync(CurrentUserId, id, attachmentId);
                return File(stream, contentType, fileName);
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        }

        [HttpDelete("{id:guid}/attachments/{attachmentId:guid}")]
        public async Task<ActionResult> DeleteAttachment(Guid id, Guid attachmentId)
        {
            try { await _spaces.DeleteAttachmentAsync(CurrentUserId, id, attachmentId); return Ok(new { message = "Deleted" }); }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        }
    }
}
