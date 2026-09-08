using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;

namespace CmdNext.Service.Contracts
{
    public interface IDTaskAiTools
    {
        /// <summary>
        /// Builds the tool list with descriptions reflecting the user's current statuses
        /// and tags, so the model only ever names ones that exist.
        /// </summary>
        Task<IReadOnlyList<AITool>> GetToolsAsync();
    }
}
