namespace CmdNext.AI.Service.Generic.Contracts
{
    public sealed class AiUsage
    {
        public int InputTokens { get; init; }

        public int OutputTokens { get; init; }

        public int TotalTokens { get; init; }
    }

    public enum AiStreamPhase
    {
        Compacting = 0,
        Generating = 1,
        Completed = 2
    }
}
