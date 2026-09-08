using System;

namespace CmdNext.Service.Contracts
{
    public interface IDTaskAiToolsFactory
    {
        IDTaskAiTools Create(Guid userId);
    }
}
