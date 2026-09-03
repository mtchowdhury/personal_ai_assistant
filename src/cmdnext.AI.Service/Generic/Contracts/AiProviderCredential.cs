namespace CmdNext.AI.Service.Generic.Contracts
{
    /// <summary>
    /// A resolved, ready-to-use provider credential. The caller (Service layer) is
    /// responsible for loading it from storage and decrypting the key before passing
    /// it in. This layer never touches the database or the crypto helper.
    /// </summary>
    public sealed class AiProviderCredential
    {
        public required string Provider { get; init; }

        public required string ApiKey { get; init; }

        public string? Model { get; init; }

        public string? Endpoint { get; init; }

        /// <summary>
        /// Stable key used to pool the constructed client, typically "{userId}:{provider}".
        /// Must change (or be evicted) when the underlying credential is rotated.
        /// </summary>
        public required string CacheKey { get; init; }
    }
}
