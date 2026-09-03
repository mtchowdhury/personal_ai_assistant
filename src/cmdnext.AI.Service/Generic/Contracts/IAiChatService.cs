using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CmdNext.AI.Service.Generic.Contracts
{
    public interface IAiChatService
    {
        Task<AiChatResponse> CompleteAsync(
            AiChatRequest request,
            CancellationToken cancellationToken = default);

        IAsyncEnumerable<AiChatStreamUpdate> StreamAsync(
            AiChatRequest request,
            CancellationToken cancellationToken = default);

        Task<string?> SummarizeAsync(
            AiSummarizeRequest request,
            CancellationToken cancellationToken = default);
    }
}
