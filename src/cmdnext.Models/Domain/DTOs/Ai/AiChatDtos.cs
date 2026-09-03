using System;
using System.Collections.Generic;

namespace CmdNext.Models.Domain.DTOs.Ai
{
    public class AiChatSessionDto
    {
        public Guid Id { get; set; }

        public string? Title { get; set; }

        public DateTime? LastMessageAt { get; set; }

        public DateTime? CreatedOn { get; set; }

        public string? Provider { get; set; }

        public string? Model { get; set; }

        /// <summary>The space this chat is scoped to, if any.</summary>
        public Guid? SpaceId { get; set; }
    }

    public class AiChatSessionDetailDto : AiChatSessionDto
    {
        public List<AiChatMessageDto> Messages { get; set; } = new();
    }

    public class AiChatMessageDto
    {
        public Guid Id { get; set; }

        public string Role { get; set; } = string.Empty;

        public string? Content { get; set; }

        public int Sequence { get; set; }

        public bool IsError { get; set; }

        public DateTime? CreatedOn { get; set; }

        public List<string> Attachments { get; set; } = new();

        public string? FinishReason { get; set; }

        public int? InputTokens { get; set; }

        public int? OutputTokens { get; set; }
    }

    public class CreateAiChatSessionRequest
    {
        public string? Title { get; set; }

        public string? ProfileName { get; set; }

        /// <summary>Scope this chat to a space; its conventions/state/tree are added to the system prompt.</summary>
        public Guid? SpaceId { get; set; }
    }

    public class SendAiChatMessageRequest
    {
        public string Message { get; set; } = string.Empty;

        public List<AiChatAttachmentDto>? Attachments { get; set; }
    }

    public class AiChatAttachmentDto
    {
        public string FileName { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty;
    }

    public class AiChatMessageAttachment
    {
        public string FileName { get; set; } = string.Empty;

        public string? Markdown { get; set; }

        public string? MediaType { get; set; }

        public string? Data { get; set; }
    }

    public class RenameAiChatSessionRequest
    {
        public string Title { get; set; } = string.Empty;
    }

    public class AiChatStreamUpdate
    {
        public int Phase { get; set; } = 1; // 0=Compacting, 1=Generating, 2=Completed

        public string? TextDelta { get; set; }

        public bool IsFinal { get; set; }

        public AiUsage? Usage { get; set; }

        public string? FinishReason { get; set; }
    }

    public class AiUsage
    {
        public int InputTokens { get; set; }

        public int OutputTokens { get; set; }

        public int TotalTokens { get; set; }
    }
}
