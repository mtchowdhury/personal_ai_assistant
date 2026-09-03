using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using CmdNext.Service.Contracts;

namespace CmdNext.Service.Tools
{
    public class SpacesAiToolProvider : IAiToolProvider
    {
        private readonly ISpaceService _spaces;
        private readonly ILogger<SpacesAiTools> _logger;

        public SpacesAiToolProvider(ISpaceService spaces, ILogger<SpacesAiTools> logger)
        {
            _spaces = spaces;
            _logger = logger;
        }

        public Task<IReadOnlyList<AITool>> GetToolsAsync(Guid userId, AiToolContext context)
        {
            var tools = new SpacesAiTools(_spaces, _logger, userId, context.SpaceId, context.PendingAttachments);
            return tools.GetToolsAsync();
        }
    }
}
