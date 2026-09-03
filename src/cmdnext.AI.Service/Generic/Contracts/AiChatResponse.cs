namespace CmdNext.AI.Service.Generic.Contracts
{
    public sealed class AiChatResponse
    {
        public string? Text { get; init; }

        public AiUsage? Usage { get; init; }

        public string? Provider { get; init; }

        public string? Model { get; init; }

        public string? FinishReason { get; init; }
    }

    /// <summary>
    /// One chunk of a streaming response. Text arrives incrementally during
    /// <see cref="AiStreamPhase.Generating"/>; the final chunk carries usage.
    /// </summary>
    public sealed class AiChatStreamUpdate
    {
        public AiStreamPhase Phase { get; init; } = AiStreamPhase.Generating;

        public string? TextDelta { get; init; }

        public bool IsFinal { get; init; }

        public AiUsage? Usage { get; init; }

        public string? FinishReason { get; init; }
    }
}
