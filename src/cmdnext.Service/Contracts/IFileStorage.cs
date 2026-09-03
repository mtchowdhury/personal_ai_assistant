using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CmdNext.Service.Contracts
{
    public class StoredFile
    {
        /// <summary>"local" now; "onedrive"/"gdrive" later.</summary>
        public string Provider { get; set; } = "local";

        /// <summary>Relative path inside the provider; store this, not an absolute path.</summary>
        public string Path { get; set; } = string.Empty;

        public long SizeBytes { get; set; }
    }

    /// <summary>
    /// Abstraction over where uploaded files live. The database always stores
    /// (Provider, Path) — never an absolute filesystem path — so a future move to
    /// OneDrive/Google Drive only needs a new implementation of this interface.
    /// </summary>
    public interface IFileStorage
    {
        /// <summary>Provider key this implementation writes as, e.g. "local".</summary>
        string Provider { get; }

        /// <summary>
        /// Saves content under a namespaced area (e.g. "spaces/{spaceId}" or "receipts"),
        /// scoped further by userId, with a generated file name based on the original.
        /// </summary>
        Task<StoredFile> SaveAsync(System.Guid userId, string area, string fileName, Stream content, CancellationToken cancellationToken = default);

        Task<Stream> OpenAsync(string relativePath, CancellationToken cancellationToken = default);

        Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default);
    }
}
