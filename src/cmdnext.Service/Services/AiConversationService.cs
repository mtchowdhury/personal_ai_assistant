using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using CmdNext.AI.Service.Generic.Configuration;
using CmdNext.AI.Service.Generic.Contracts;
using CmdNext.Models.Domain.DTOs.Ai;
using CmdNext.Models.Domain.Model.App.Ai;
using CmdNext.Repository.Contracts;
using CmdNext.Service.Contracts;
using CmdNext.Service.Helpers;
using CmdNext.Service.Logging;

namespace CmdNext.Service.Services
{
    public class AiConversationService : IAiConversationService
    {
        private const int TitleMaxLength = 60;

        private readonly IUnitOfWork _unitOfWork;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IAiCredentialResolver _credentialResolver;
        private readonly IAiChatService _aiChatService;
        private readonly AiOptions _options;
        private readonly ILogger<AiConversationService> _logger;
        private readonly IEnumerable<IAiToolProvider> _toolProviders;
        private readonly ISpaceService _spaces;

        public AiConversationService(
            IUnitOfWork unitOfWork,
            IHttpContextAccessor httpContextAccessor,
            IAiCredentialResolver credentialResolver,
            IAiChatService aiChatService,
            IOptions<AiOptions> options,
            ILogger<AiConversationService> logger,
            IEnumerable<IAiToolProvider> toolProviders,
            ISpaceService spaces)
        {
            _unitOfWork = unitOfWork;
            _httpContextAccessor = httpContextAccessor;
            _credentialResolver = credentialResolver;
            _aiChatService = aiChatService;
            _options = options.Value;
            _logger = logger;
            _toolProviders = toolProviders;
            _spaces = spaces;
        }

        public async Task<AiChatSessionDto> CreateSessionAsync(Guid userId, string? title = null, string? profileName = null, Guid? spaceId = null)
        {
            var session = new AiChatSession
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Title = string.IsNullOrWhiteSpace(title) ? null : Truncate(title, TitleMaxLength),
                ProfileName = string.IsNullOrWhiteSpace(profileName) ? _options.DefaultProfile : profileName,
                SpaceId = spaceId,
                CreatedOn = DateTime.UtcNow
            };

            await _unitOfWork.Repository<AiChatSession, Guid>().AddAsync(session);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation(
                "Created chat session {SessionId} for user {UserId} with profile {ProfileName}",
                session.Id, userId, session.ProfileName);

            return ToDto(session);
        }

        public async Task<List<AiChatSessionDto>> GetSessionsAsync(Guid userId)
        {
            var sessions = await _unitOfWork.Repository<AiChatSession, Guid>()
                .FindAsync(x => x.UserId == userId, asNoTracking: true);

            return sessions
                .OrderByDescending(x => x.LastMessageAt ?? x.CreatedOn)
                .Select(ToDto)
                .ToList();
        }

        public async Task<AiChatSessionDetailDto> GetSessionAsync(Guid sessionId, Guid userId)
        {
            var session = await LoadOwnedSessionAsync(sessionId, userId);

            var messages = await _unitOfWork.Repository<AiChatMessage, Guid>()
                .FindAsync(x => x.SessionId == sessionId, asNoTracking: true);

            return new AiChatSessionDetailDto
            {
                Id = session.Id,
                Title = session.Title,
                LastMessageAt = session.LastMessageAt,
                CreatedOn = session.CreatedOn,
                Provider = session.Provider,
                Model = session.Model,
                Messages = messages
                    .OrderBy(x => x.Sequence)
                    .Select(x => new AiChatMessageDto
                    {
                        Id = x.Id,
                        Role = x.Role,
                        Content = x.Content,
                        Sequence = x.Sequence,
                        IsError = x.IsError,
                        CreatedOn = x.CreatedOn,
                        Attachments = ParseAttachments(x.AttachmentsJson)
                            .Select(a => a.FileName)
                            .ToList(),
                        FinishReason = x.FinishReason,
                        InputTokens = x.InputTokens,
                        OutputTokens = x.OutputTokens
                    })
                    .ToList()
            };
        }

        public async Task RenameSessionAsync(Guid sessionId, Guid userId, string title)
        {
            _logger.LogInformation("Renaming session {SessionId} for user {UserId}", sessionId, userId);

            var session = await LoadOwnedSessionAsync(sessionId, userId);

            session.Title = Truncate(title, TitleMaxLength);
            session.UpdatedOn = DateTime.UtcNow;

            _unitOfWork.Repository<AiChatSession, Guid>().Update(session);
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task DeleteSessionAsync(Guid sessionId, Guid userId)
        {
            _logger.LogInformation("Deleting session {SessionId} for user {UserId}", sessionId, userId);

            var session = await LoadOwnedSessionAsync(sessionId, userId);

            _unitOfWork.Repository<AiChatSession, Guid>().Delete(session);
            await _unitOfWork.SaveChangesAsync();
        }

        public async IAsyncEnumerable<CmdNext.AI.Service.Generic.Contracts.AiChatStreamUpdate> SendMessageAsync(
            Guid sessionId,
            Guid userId,
            string message,
            List<AiChatAttachmentDto>? attachments = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            _logger.LogInformation(
                "Starting AI turn for session {SessionId}, user {UserId}, {AttachmentCount} attachment(s)",
                sessionId, userId, attachments?.Count ?? 0);

            var session = await LoadOwnedSessionAsync(sessionId, userId);
            var credential = await _credentialResolver.ResolveAsync(userId);

            var convertedAttachments = ConvertAttachments(attachments);

            var history = (await _unitOfWork.Repository<AiChatMessage, Guid>()
                    .FindAsync(x => x.SessionId == sessionId, asNoTracking: true))
                .OrderBy(x => x.Sequence)
                .ToList();

            var nextSequence = history.Count == 0 ? 1 : history[^1].Sequence + 1;

            var userMessage = new AiChatMessage
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                Role = "user",
                Content = message,
                AttachmentsJson = convertedAttachments.Count == 0
                    ? null
                    : JsonSerializer.Serialize(convertedAttachments),
                Sequence = nextSequence,
                CreatedOn = DateTime.UtcNow
            };

            await _unitOfWork.Repository<AiChatMessage, Guid>().AddAsync(userMessage);

            if (string.IsNullOrWhiteSpace(session.Title))
            {
                session.Title = Truncate(message, TitleMaxLength);
            }

            session.LastMessageAt = DateTime.UtcNow;
            session.Provider = credential.Provider;
            session.Model = credential.Model;
            _unitOfWork.Repository<AiChatSession, Guid>().Update(session);
            await _unitOfWork.SaveChangesAsync();

            history.Add(userMessage);

            if (ShouldCompact(session, history))
            {
                _logger.LogInformation(
                    "Compaction threshold reached for session {SessionId} ({MessageCount} messages)",
                    sessionId, history.Count);

                yield return new CmdNext.AI.Service.Generic.Contracts.AiChatStreamUpdate { Phase = AiStreamPhase.Compacting };

                await CompactAsync(session, history, credential, cancellationToken);
            }

            var pending = history
                .Where(x => session.SummarizedUpToMessageId == null
                            || x.Sequence > SequenceOf(history, session.SummarizedUpToMessageId.Value))
                .ToList();

            var toolContext = new AiToolContext(session.SpaceId, convertedAttachments);
            var tools = new List<Microsoft.Extensions.AI.AITool>();
            foreach (var provider in _toolProviders)
            {
                tools.AddRange(await provider.GetToolsAsync(userId, toolContext));
            }

            var request = new AiChatRequest
            {
                Credential = credential,
                Messages = ToChatMessages(pending),
                SystemPrompt = await ResolveSystemPromptAsync(userId, session),
                ConversationSummary = session.SummaryText,
                ProfileName = session.ProfileName,
                Tools = tools
            };

            var buffer = new StringBuilder();
            CmdNext.AI.Service.Generic.Contracts.AiUsage? usage = null;
            string? finishReason = null;
            var stopwatch = Stopwatch.StartNew();
            var streamFailed = false;
            string? failureReason = null;

            _logger.LogDebug(
                "Dispatching {MessageCount} message(s) to provider {Provider} model {Model} for session {SessionId}",
                request.Messages.Count, credential.Provider, credential.Model, sessionId);

            var hasImages = request.Messages.Any(m => m.Contents.OfType<Microsoft.Extensions.AI.DataContent>().Any());

            await using var updates = _aiChatService
                .StreamAsync(request, cancellationToken)
                .GetAsyncEnumerator(cancellationToken);

            while (true)
            {
                CmdNext.AI.Service.Generic.Contracts.AiChatStreamUpdate update;

                try
                {
                    if (!await updates.MoveNextAsync())
                    {
                        break;
                    }

                    update = updates.Current;
                }
                catch (Exception ex) when (hasImages && IsImageRejection(ex))
                {
                    _logger.LogWarning(ex,
                        "Model {Model} rejected an image attachment for session {SessionId}",
                        credential.Model, sessionId);
                    throw new AiImageNotSupportedException(
                        $"The configured model '{credential.Model}' could not process the image attachment. " +
                        "Remove the image or switch to a vision-capable model.",
                        ex);
                }

                if (!string.IsNullOrEmpty(update.TextDelta))
                {
                    buffer.Append(update.TextDelta);
                }

                if (update.IsFinal)
                {
                    usage = update.Usage;
                    finishReason = update.FinishReason;
                }

                yield return update;
            }

            stopwatch.Stop();

            _logger.LogInformation(
                "AI turn completed for session {SessionId} in {ElapsedMs} ms " +
                "(provider {Provider}, model {Model}, in {InputTokens} tok, out {OutputTokens} tok, finish {FinishReason})",
                sessionId, stopwatch.ElapsedMilliseconds, credential.Provider, credential.Model,
                usage?.InputTokens ?? 0, usage?.OutputTokens ?? 0, finishReason ?? "unknown");

            if (buffer.Length == 0)
            {
                // An empty completion usually indicates a provider-side problem.
                streamFailed = true;
                failureReason = "The provider returned an empty response.";
                _logger.LogWarning(
                    "Provider {Provider} returned an empty response for session {SessionId}",
                    credential.Provider, sessionId);
            }

            var assistantMessage = new AiChatMessage
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                Role = "assistant",
                Content = buffer.ToString(),
                Sequence = nextSequence + 1,
                Provider = credential.Provider,
                Model = credential.Model,
                InputTokens = usage?.InputTokens,
                OutputTokens = usage?.OutputTokens,
                FinishReason = finishReason,
                CreatedOn = DateTime.UtcNow
            };

            await _unitOfWork.Repository<AiChatMessage, Guid>().AddAsync(assistantMessage);

            await _unitOfWork.Repository<AiUsageLog, Guid>().AddAsync(new AiUsageLog
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                SessionId = sessionId,
                MessageId = assistantMessage.Id,
                Provider = credential.Provider,
                Model = credential.Model,
                ProfileName = session.ProfileName,
                InputTokens = usage?.InputTokens ?? 0,
                OutputTokens = usage?.OutputTokens ?? 0,
                TotalTokens = usage?.TotalTokens ?? 0,
                DurationMs = (int)stopwatch.ElapsedMilliseconds,
                IsSuccess = !streamFailed,
                ErrorMessage = failureReason,
                CreatedOn = DateTime.UtcNow
            });

            session.LastMessageAt = DateTime.UtcNow;
            _unitOfWork.Repository<AiChatSession, Guid>().Update(session);

            await _unitOfWork.SaveChangesAsync();
        }

        public async Task<bool> CompactSessionAsync(Guid sessionId, Guid userId, CancellationToken cancellationToken = default)
        {
            var session = await LoadOwnedSessionAsync(sessionId, userId);
            var credential = await _credentialResolver.ResolveAsync(userId);

            var history = (await _unitOfWork.Repository<AiChatMessage, Guid>()
                    .FindAsync(x => x.SessionId == sessionId, asNoTracking: true))
                .OrderBy(x => x.Sequence)
                .ToList();

            return await CompactAsync(session, history, credential, cancellationToken);
        }

        private async Task<bool> CompactAsync(
            AiChatSession session,
            List<AiChatMessage> history,
            AiProviderCredential credential,
            CancellationToken cancellationToken)
        {
            var alreadySummarized = session.SummarizedUpToMessageId == null
                ? 0
                : SequenceOf(history, session.SummarizedUpToMessageId.Value);

            var candidates = history
                .Where(x => x.Sequence > alreadySummarized)
                .ToList();

            var foldCount = candidates.Count - _options.CompactionKeepRecent;
            if (foldCount <= 0)
            {
                _logger.LogDebug(
                    "Skipping compaction for session {SessionId}: only {CandidateCount} candidate message(s)",
                    session.Id, candidates.Count);
                return false;
            }

            var toFold = candidates.Take(foldCount).ToList();

            using var trace = ServiceTrace.Begin(
                _logger,
                "CompactSession",
                new { SessionId = session.Id, FoldCount = foldCount, Provider = credential.Provider });

            var summary = await _aiChatService.SummarizeAsync(
                new AiSummarizeRequest
                {
                    Credential = credential,
                    Messages = ToChatMessages(toFold),
                    ExistingSummary = session.SummaryText
                },
                cancellationToken);

            if (string.IsNullOrWhiteSpace(summary))
            {
                _logger.LogWarning(
                    "Compaction produced an empty summary for session {SessionId}; keeping full history",
                    session.Id);
                return false;
            }

            session.SummaryText = summary;
            session.SummarizedUpToMessageId = toFold[^1].Id;
            session.UpdatedOn = DateTime.UtcNow;

            _unitOfWork.Repository<AiChatSession, Guid>().Update(session);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation(
                "Compacted {FoldCount} message(s) into the summary for session {SessionId}",
                foldCount, session.Id);

            return true;
        }

        private bool ShouldCompact(AiChatSession session, List<AiChatMessage> history)
        {
            if (_options.CompactionThreshold <= 0)
            {
                return false;
            }

            var alreadySummarized = session.SummarizedUpToMessageId == null
                ? 0
                : SequenceOf(history, session.SummarizedUpToMessageId.Value);

            var liveCount = history.Count(x => x.Sequence > alreadySummarized);

            return liveCount > _options.CompactionThreshold;
        }

        private async Task<string?> ResolveSystemPromptAsync(Guid userId, AiChatSession session)
        {
            var name = string.IsNullOrWhiteSpace(session.ProfileName)
                ? _options.DefaultProfile
                : session.ProfileName;

            string? basePrompt = null;
            if (!string.IsNullOrWhiteSpace(name))
            {
                basePrompt = _options.Profiles.TryGetValue(name, out var profile) ? profile.SystemPrompt : null;
            }

            if (session.SpaceId is not { } spaceId)
            {
                return basePrompt;
            }

            var spaceBlock = await BuildSpaceBlockAsync(userId, spaceId);
            return string.IsNullOrWhiteSpace(basePrompt) ? spaceBlock : $"{basePrompt}\n\n{spaceBlock}";
        }

        /// <summary>
        /// Builds the system-prompt addendum for a space-scoped chat: conventions, progress
        /// state, node tree, and entry types, so the model can call Spaces tools correctly
        /// without asking the user to repeat context every message.
        /// </summary>
        private async Task<string> BuildSpaceBlockAsync(Guid userId, Guid spaceId)
        {
            try
            {
                var space = await _spaces.GetSpaceAsync(userId, spaceId);
                var nodes = await _spaces.GetNodesAsync(userId, spaceId);

                var sb = new StringBuilder();
                sb.AppendLine($"You are currently working inside the space \"{space.Name}\" (id: {space.Id}, kind: {space.Kind}).");
                if (!string.IsNullOrWhiteSpace(space.Description)) sb.AppendLine($"Description: {space.Description}");
                if (!string.IsNullOrWhiteSpace(space.Conventions)) sb.AppendLine($"Conventions: {space.Conventions}");
                sb.AppendLine($"Progress state: {space.StateJson}");
                sb.AppendLine($"Entry types and their fields: {space.SchemaJson}");
                sb.AppendLine(nodes.Count == 0
                    ? "Node tree: (empty — create nodes with add_node as needed)"
                    : "Node tree:\n" + string.Join("\n", nodes.Take(200).Select(n => $"  {n.Path} (id: {n.Id})")));
                sb.AppendLine(
                    "Use the Spaces tools (list_spaces, get_space, search_entries, get_entry, list_entries, " +
                    "add_entry, append_to_entry, add_node, update_space_state, save_attachment) for anything about " +
                    "this space; default spaceId to this space unless the user clearly means another one. " +
                    "Only call save_attachment when the user explicitly asks to save/keep/file an attachment — " +
                    "most attached images or files are for this reply only and should not be saved.");

                return sb.ToString();
            }
            catch (KeyNotFoundException)
            {
                return string.Empty;
            }
        }

        private async Task<AiChatSession> LoadOwnedSessionAsync(Guid sessionId, Guid userId)
        {
            var session = (await _unitOfWork.Repository<AiChatSession, Guid>()
                    .FindAsync(x => x.Id == sessionId && x.UserId == userId))
                .FirstOrDefault();

            if (session == null)
            {
                // Either the session does not exist or it belongs to another user;
                // both are worth recording as denied access.
                _logger.LogWarning(
                    "Denied access to session {SessionId} for user {UserId}: not found or not owned",
                    sessionId, userId);
                throw new UnauthorizedAccessException("Chat session not found.");
            }

            return session;
        }

        private static bool IsImageRejection(Exception ex)
        {
            for (var current = ex; current != null; current = current.InnerException)
            {
                if (current.Message.Contains("image", StringComparison.OrdinalIgnoreCase)
                    || current.Message.Contains("vision", StringComparison.OrdinalIgnoreCase)
                    || current.Message.Contains("multimodal", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static int SequenceOf(List<AiChatMessage> history, Guid messageId)
        {
            return history.FirstOrDefault(x => x.Id == messageId)?.Sequence ?? 0;
        }

        private static List<Microsoft.Extensions.AI.ChatMessage> ToChatMessages(IEnumerable<AiChatMessage> messages)
        {
            return messages
                .Where(x => !x.IsError
                            && (!string.IsNullOrWhiteSpace(x.Content)
                                || !string.IsNullOrWhiteSpace(x.AttachmentsJson)))
                .Select(ToChatMessage)
                .ToList();
        }

        private static Microsoft.Extensions.AI.ChatMessage ToChatMessage(AiChatMessage message)
        {
            var role = string.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase)
                ? Microsoft.Extensions.AI.ChatRole.Assistant
                : Microsoft.Extensions.AI.ChatRole.User;

            var attachments = ParseAttachments(message.AttachmentsJson);

            if (attachments.Count == 0)
            {
                return new Microsoft.Extensions.AI.ChatMessage(role, message.Content);
            }

            var sb = new StringBuilder(message.Content ?? string.Empty);

            foreach (var attachment in attachments.Where(a => !string.IsNullOrEmpty(a.Markdown)))
            {
                sb.AppendLine();
                sb.AppendLine();
                sb.AppendLine($"--- Begin content of attached file \"{attachment.FileName}\" (converted to Markdown) ---");
                sb.AppendLine();
                sb.AppendLine(attachment.Markdown!.TrimEnd());
                sb.AppendLine();
                sb.AppendLine($"--- End content of attached file \"{attachment.FileName}\" ---");
            }

            var contents = new List<Microsoft.Extensions.AI.AIContent>();

            var text = sb.ToString();
            if (!string.IsNullOrWhiteSpace(text))
            {
                contents.Add(new Microsoft.Extensions.AI.TextContent(text));
            }

            foreach (var attachment in attachments.Where(a =>
                         !string.IsNullOrEmpty(a.Data) && !string.IsNullOrEmpty(a.MediaType)))
            {
                contents.Add(new Microsoft.Extensions.AI.DataContent(
                    Convert.FromBase64String(attachment.Data!), 
                    attachment.MediaType!));
            }

            return new Microsoft.Extensions.AI.ChatMessage(role, contents);
        }

        private static List<AiChatMessageAttachment> ConvertAttachments(List<AiChatAttachmentDto>? attachments)
        {
            if (attachments is not { Count: > 0 })
            {
                return new List<AiChatMessageAttachment>();
            }

            return attachments
                .Select(a => AiAttachmentHelper.ToAttachment(a.FileName, Convert.FromBase64String(a.Content)))
                .ToList();
        }

        private static List<AiChatMessageAttachment> ParseAttachments(string? attachmentsJson)
        {
            if (string.IsNullOrWhiteSpace(attachmentsJson))
            {
                return new List<AiChatMessageAttachment>();
            }

            return JsonSerializer.Deserialize<List<AiChatMessageAttachment>>(attachmentsJson)
                   ?? new List<AiChatMessageAttachment>();
        }

        private static AiChatSessionDto ToDto(AiChatSession session)
        {
            return new AiChatSessionDto
            {
                Id = session.Id,
                Title = session.Title,
                LastMessageAt = session.LastMessageAt,
                CreatedOn = session.CreatedOn,
                Provider = session.Provider,
                Model = session.Model,
                SpaceId = session.SpaceId
            };
        }

        private static string Truncate(string value, int maxLength)
        {
            value = value.Trim();
            return value.Length <= maxLength ? value : value[..maxLength] + "...";
        }
    }
}
