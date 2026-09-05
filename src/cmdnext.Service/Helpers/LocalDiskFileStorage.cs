using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using CmdNext.Service.Contracts;

namespace CmdNext.Service.Helpers
{
    /// <summary>
    /// Stores files under "{ContentRoot}/storage/{area}/{userId}/{fileName}" on local disk.
    /// The "storage" folder is gitignored; relocating to cloud later means adding a new
    /// <see cref="IFileStorage"/> implementation, not changing any caller.
    /// </summary>
    public class LocalDiskFileStorage : IFileStorage
    {
        private readonly IHostEnvironment _environment;

        public LocalDiskFileStorage(IHostEnvironment environment)
        {
            _environment = environment;
        }

        public string Provider => "local";

        public async Task<StoredFile> SaveAsync(Guid userId, string area, string fileName, Stream content, CancellationToken cancellationToken = default)
        {
            var safeArea = area.Trim('/', '\\');
            var relativeDir = Path.Combine(safeArea.Split('/'));
            relativeDir = Path.Combine(relativeDir, userId.ToString());

            var absoluteDir = Path.Combine(GetStorageRoot(), relativeDir);
            Directory.CreateDirectory(absoluteDir);

            var extension = Path.GetExtension(fileName);
            var storedName = $"{Guid.NewGuid()}{extension}";
            var absolutePath = Path.Combine(absoluteDir, storedName);

            await using (var fileStream = new FileStream(absolutePath, FileMode.Create))
            {
                await content.CopyToAsync(fileStream, cancellationToken);
            }

            var relativePath = Path.Combine(relativeDir, storedName).Replace('\\', '/');
            return new StoredFile
            {
                Provider = Provider,
                Path = relativePath,
                SizeBytes = new FileInfo(absolutePath).Length
            };
        }

        public Task<Stream> OpenAsync(string relativePath, CancellationToken cancellationToken = default)
        {
            var absolutePath = ResolveWithinRoot(relativePath);
            if (!File.Exists(absolutePath))
                throw new FileNotFoundException("Stored file not found.", relativePath);

            Stream stream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read);
            return Task.FromResult(stream);
        }

        public Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
        {
            var absolutePath = ResolveWithinRoot(relativePath);
            if (File.Exists(absolutePath)) File.Delete(absolutePath);
            return Task.CompletedTask;
        }

        private string GetStorageRoot()
        {
            var root = Path.Combine(_environment.ContentRootPath, "storage");
            Directory.CreateDirectory(root);
            return root;
        }

        /// <summary>
        /// Resolves a stored relative path and refuses anything that escapes the storage
        /// root. Today every caller passes a path read back from the database, but that is
        /// one careless controller away from being attacker-controlled.
        /// </summary>
        private string ResolveWithinRoot(string relativePath)
        {
            var root = GetStorageRoot();
            var full = Path.GetFullPath(Path.Combine(root, relativePath));
            var rootPrefix = Path.GetFullPath(root) + Path.DirectorySeparatorChar;

            if (!full.StartsWith(rootPrefix, StringComparison.Ordinal))
                throw new UnauthorizedAccessException($"Path escapes storage root: {relativePath}");

            return full;
        }
    }
}
