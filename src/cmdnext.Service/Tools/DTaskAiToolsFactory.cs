using System;
using Microsoft.Extensions.Logging;
using CmdNext.Service.Contracts;

namespace CmdNext.Service.Tools
{
    public class DTaskAiToolsFactory : IDTaskAiToolsFactory
    {
        private readonly IDTaskService _tasks;
        private readonly ILogger<DTaskAiTools> _logger;

        public DTaskAiToolsFactory(IDTaskService tasks, ILogger<DTaskAiTools> logger)
        {
            _tasks = tasks;
            _logger = logger;
        }

        public IDTaskAiTools Create(Guid userId) => new DTaskAiTools(_tasks, _logger, userId);
    }
}
