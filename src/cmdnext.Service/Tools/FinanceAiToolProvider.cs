using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using CmdNext.Service.Contracts;

namespace CmdNext.Service.Tools
{
    /// <summary>Adapts the existing finance tools factory to <see cref="IAiToolProvider"/> unchanged.</summary>
    public class FinanceAiToolProvider : IAiToolProvider
    {
        private readonly IFinanceAiToolsFactory _factory;

        public FinanceAiToolProvider(IFinanceAiToolsFactory factory)
        {
            _factory = factory;
        }

        public Task<IReadOnlyList<AITool>> GetToolsAsync(Guid userId, AiToolContext context) =>
            _factory.Create(userId).GetToolsAsync();
    }
}
