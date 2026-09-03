using System;
using Microsoft.Extensions.Logging;
using CmdNext.Service.Contracts;

namespace CmdNext.Service.Tools
{
    public class FinanceAiToolsFactory : IFinanceAiToolsFactory
    {
        private readonly IFinanceService _finance;
        private readonly ILogger<FinanceAiTools> _logger;

        public FinanceAiToolsFactory(IFinanceService finance, ILogger<FinanceAiTools> logger)
        {
            _finance = finance;
            _logger = logger;
        }

        public IFinanceAiTools Create(Guid userId) => new FinanceAiTools(_finance, _logger, userId);
    }
}
