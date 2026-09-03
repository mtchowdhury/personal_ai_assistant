using System;

namespace CmdNext.Service.Contracts
{
    public interface IFinanceAiToolsFactory
    {
        IFinanceAiTools Create(Guid userId);
    }
}
