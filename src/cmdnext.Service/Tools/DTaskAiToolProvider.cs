using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using CmdNext.Service.Contracts;

namespace CmdNext.Service.Tools
{
    /// <summary>Exposes the daily-task tools to the chat turn via <see cref="IAiToolProvider"/>.</summary>
    public class DTaskAiToolProvider : IAiToolProvider
    {
        private readonly IDTaskAiToolsFactory _factory;

        public DTaskAiToolProvider(IDTaskAiToolsFactory factory)
        {
            _factory = factory;
        }

        public Task<IReadOnlyList<AITool>> GetToolsAsync(Guid userId, AiToolContext context) =>
            _factory.Create(userId).GetToolsAsync();
    }
}
