namespace CmdNext.AI.Service.Generic.Configuration
{
    /// <summary>
    /// Infrastructure-level settings for a provider. Deliberately holds no credentials,
    /// no model, and nothing tenant-specific — those come from the user's row in
    /// the ai schema. <see cref="IsEnabled"/> is a system-wide kill switch.
    /// </summary>
    public class ProviderOptions
    {
        public string? Endpoint { get; set; }

        public bool IsEnabled { get; set; } = true;
    }
}
