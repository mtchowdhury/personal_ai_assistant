using System;
using System.Text.Json;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CmdNext.Api.Infrastructure;
using CmdNext.AI.Service.Generic.Contracts;
using CmdNext.Models.Domain.DTOs.Ai;
using CmdNext.Service.Contracts;

namespace CmdNext.Api.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Authorize]
    [Route("api/v{version:apiVersion}/[controller]")]
    public class AiController : ApiControllerBase
    {
        private static readonly JsonSerializerOptions StreamJsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly IAiConversationService _conversationService;

        public AiController(IAiConversationService conversationService)
        {
            _conversationService = conversationService;
        }

        private Guid UserId => CurrentUserId;

        [HttpGet("sessions")]
        public async Task<ActionResult<List<AiChatSessionDto>>> GetSessions()
        {
            try
            {
                return Ok(await _conversationService.GetSessionsAsync(UserId));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while fetching chat sessions", error = ex.Message });
            }
        }

        [HttpGet("sessions/{sessionId:guid}")]
        public async Task<ActionResult<AiChatSessionDetailDto>> GetSession(Guid sessionId)
        {
            try
            {
                return Ok(await _conversationService.GetSessionAsync(sessionId, UserId));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(404, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while fetching the chat session", error = ex.Message });
            }
        }

        [HttpPost("sessions")]
        public async Task<ActionResult<AiChatSessionDto>> CreateSession([FromBody] CreateAiChatSessionRequest request)
        {
            try
            {
                var session = await _conversationService.CreateSessionAsync(UserId, request.Title, request.ProfileName, request.SpaceId);
                return Ok(session);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while creating the chat session", error = ex.Message });
            }
        }

        [HttpPut("sessions/{sessionId:guid}/title")]
        public async Task<IActionResult> RenameSession(Guid sessionId, [FromBody] RenameAiChatSessionRequest request)
        {
            try
            {
                await _conversationService.RenameSessionAsync(sessionId, UserId, request.Title);
                return Ok(new { message = "Session renamed" });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(404, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while renaming the chat session", error = ex.Message });
            }
        }

        [HttpDelete("sessions/{sessionId:guid}")]
        public async Task<IActionResult> DeleteSession(Guid sessionId)
        {
            try
            {
                await _conversationService.DeleteSessionAsync(sessionId, UserId);
                return Ok(new { message = "Session deleted" });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(404, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while deleting the chat session", error = ex.Message });
            }
        }

        [HttpPost("sessions/{sessionId:guid}/compact")]
        public async Task<IActionResult> CompactSession(Guid sessionId, CancellationToken cancellationToken)
        {
            try
            {
                var compacted = await _conversationService.CompactSessionAsync(sessionId, UserId, cancellationToken);
                return Ok(new { compacted });
            }
            catch (AiNotConfiguredException ex)
            {
                return StatusCode(409, new { code = "ai_not_configured", message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(404, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while compacting the chat session", error = ex.Message });
            }
        }

        /// <summary>
        /// Streams the assistant reply as Server-Sent Events. Each event is a JSON
        /// <see cref="CmdNext.Models.Domain.DTOs.Ai.AiChatStreamUpdate"/>; the stream ends with a `done` event.
        /// </summary>
        [HttpPost("sessions/{sessionId:guid}/messages/stream")]
        public async Task StreamMessage(
            Guid sessionId,
            [FromBody] SendAiChatMessageRequest request,
            CancellationToken cancellationToken)
        {
            Response.Headers.ContentType = "text/event-stream";
            Response.Headers.CacheControl = "no-cache";
            Response.Headers.Connection = "keep-alive";
            Response.Headers["X-Accel-Buffering"] = "no";

            try
            {
                var stream = _conversationService.SendMessageAsync(sessionId, UserId, request.Message, request.Attachments, cancellationToken);

                await foreach (var update in stream.WithCancellation(cancellationToken))
                {
                    await WriteEventAsync("message", update, cancellationToken);
                }

                await WriteRawEventAsync("done", "{}", cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Client disconnected — nothing to report.
            }
            catch (AiNotConfiguredException ex)
            {
                await WriteRawEventAsync(
                    "error",
                    JsonSerializer.Serialize(new { code = "ai_not_configured", message = ex.Message }, StreamJsonOptions),
                    cancellationToken);
            }
            catch (AiCredentialRejectedException ex)
            {
                await WriteRawEventAsync(
                    "error",
                    JsonSerializer.Serialize(new { code = "ai_credential_rejected", message = ex.Message }, StreamJsonOptions),
                    cancellationToken);
            }
            catch (AiImageNotSupportedException ex)
            {
                await WriteRawEventAsync(
                    "error",
                    JsonSerializer.Serialize(new { code = "ai_image_not_supported", message = ex.Message }, StreamJsonOptions),
                    cancellationToken);
            }
            catch (UnauthorizedAccessException ex)
            {
                await WriteRawEventAsync(
                    "error",
                    JsonSerializer.Serialize(new { code = "not_found", message = ex.Message }, StreamJsonOptions),
                    cancellationToken);
            }
            catch (Exception ex)
            {
                await WriteRawEventAsync(
                    "error",
                    JsonSerializer.Serialize(new { code = "ai_error", message = ex.Message }, StreamJsonOptions),
                    cancellationToken);
            }
        }

        private Task WriteEventAsync<T>(string eventName, T payload, CancellationToken cancellationToken)
        {
            return WriteRawEventAsync(eventName, JsonSerializer.Serialize(payload, StreamJsonOptions), cancellationToken);
        }

        private async Task WriteRawEventAsync(string eventName, string json, CancellationToken cancellationToken)
        {
            await Response.WriteAsync($"event: {eventName}\n", cancellationToken);
            await Response.WriteAsync($"data: {json}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
    }
}
