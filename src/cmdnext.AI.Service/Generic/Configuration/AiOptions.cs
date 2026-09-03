using System.Collections.Generic;

namespace CmdNext.AI.Service.Generic.Configuration
{
    public class AiOptions
    {
        public const string SectionName = "AI";

        /// <summary>
        /// Messages kept verbatim in the outgoing request. Older messages are folded
        /// into the session summary once <see cref="CompactionThreshold"/> is passed.
        /// </summary>
        public int MaxHistoryMessages { get; set; } = 40;

        /// <summary>
        /// Message count at which the session is compacted before the next send.
        /// </summary>
        public int CompactionThreshold { get; set; } = 30;

        /// <summary>
        /// Most recent messages left untouched by compaction.
        /// </summary>
        public int CompactionKeepRecent { get; set; } = 10;

        public int DefaultMaxOutputTokens { get; set; } = 4096;

        public float DefaultTemperature { get; set; } = 0.7f;

        public int RequestTimeoutSeconds { get; set; } = 300;

        public int ClientCacheMinutes { get; set; } = 20;

        /// <summary>
        /// Profile applied to sessions created without an explicit one.
        /// </summary>
        public string DefaultProfile { get; set; } = "Assistant";

        public Dictionary<string, ProviderOptions> Providers { get; set; } = new();

        public Dictionary<string, ProfileOptions> Profiles { get; set; } = new();
    }

    public class EmbeddingOptions
    {
        public const string SectionName = "AI:Embedding";

        public string? Provider { get; set; } = "mistral";

        public string? Model { get; set; } = "mistral-embed";

        public int Dimensions { get; set; } = 1024;

        public string? ApiKey { get; set; }

        public int ChunkSize { get; set; } = 700;

        public int ChunkOverlap { get; set; } = 100;

        public int TopK { get; set; } = 5;

        public double MinScore { get; set; } = 0.65;
    }
}
