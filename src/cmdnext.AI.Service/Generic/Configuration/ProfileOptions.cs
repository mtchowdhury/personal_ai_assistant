using System.Collections.Generic;

namespace CmdNext.AI.Service.Generic.Configuration
{
    /// <summary>
    /// Product-level behaviour for a named use case (personal assistant, data analysis).
    /// Holds no credentials and no model — a user's row supplies those.
    /// </summary>
    public class ProfileOptions
    {
        public string? SystemPrompt { get; set; }

        public float? Temperature { get; set; }

        public int? MaxOutputTokens { get; set; }

        public int? MaxHistoryMessages { get; set; }

        public List<string> Tools { get; set; } = new();
    }
}
