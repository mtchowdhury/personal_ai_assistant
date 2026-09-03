using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;

namespace CmdNext.Service.Contracts
{
    public interface IFinanceAiTools
    {
        /// <summary>
        /// Builds the tool list with descriptions reflecting the user's current categories,
        /// so the model knows about any custom categories they've added.
        /// </summary>
        Task<IReadOnlyList<AITool>> GetToolsAsync();
    }
}
