using System;

namespace CmdNext.AI.Service.Generic.Contracts
{
    /// <summary>
    /// Raised when a user has no usable AI configuration. Surfaces to the client
    /// as a "configure your AI provider" prompt rather than a generic failure.
    /// </summary>
    public class AiNotConfiguredException : Exception
    {
        public AiNotConfiguredException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// Raised when the provider rejects the user's credential (revoked, expired,
    /// or out of quota). Distinct from <see cref="AiNotConfiguredException"/> because
    /// the remedy differs: the key exists but no longer works.
    /// </summary>
    public class AiCredentialRejectedException : Exception
    {
        public string? Provider { get; }

        public AiCredentialRejectedException(string message, string? provider = null, Exception? inner = null)
            : base(message, inner)
        {
            Provider = provider;
        }
    }

    /// <summary>
    /// Raised when the provider rejects a request because the configured model cannot
    /// process image input. Surfaces to the client as an actionable message rather
    /// than the provider's raw validation error.
    /// </summary>
    public class AiImageNotSupportedException : Exception
    {
        public AiImageNotSupportedException(string message, Exception? inner = null)
            : base(message, inner)
        {
        }
    }
}
