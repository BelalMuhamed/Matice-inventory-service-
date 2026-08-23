namespace invetoryBackGroundServices.Options
{
    /// <summary>
    /// Settings for communicating with the physical card machine, bound from the
    /// <c>"MachineCommunication"</c> section.
    /// <para>
    /// Backend configuration rather than request data: a timeout is a property of the deployment
    /// environment, not something a caller should be able to set per request. This also resolves
    /// a pre-existing inconsistency - the old <c>httpPOSTGetInfoJson</c> set a 10-second timeout
    /// while <c>httpPOST</c> had its timeout line commented out entirely, silently falling back
    /// to the .NET default of roughly 100 seconds.
    /// </para>
    /// </summary>
    public sealed class MachineCommunicationOptions
    {
        /// <summary>Configuration section name these options bind from.</summary>
        public const string SectionName = "MachineCommunication";

        /// <summary>
        /// How long to wait for the machine to answer a single command, in seconds. Defaults to
        /// 10, matching the only timeout the pre-existing code actually set.
        /// </summary>
        public int TimeoutSeconds { get; set; } = 10;
    }

    /// <summary>
    /// Cross-origin settings, bound from the <c>"Cors"</c> section. Replaces the previous
    /// <c>AllowAnyOrigin</c> policy, which allowed any page loaded in any browser that could
    /// reach this service to invoke commands that physically move and emboss cards.
    /// </summary>
    public sealed class CorsPolicyOptions
    {
        /// <summary>Configuration section name these options bind from.</summary>
        public const string SectionName = "Cors";

        /// <summary>
        /// Exact origins permitted to call this service (scheme + host + port, no trailing
        /// slash), e.g. <c>https://inventory.example.com</c>. Must be non-empty; startup fails
        /// otherwise rather than silently falling back to permitting everything.
        /// </summary>
        public string[] AllowedOrigins { get; set; } = [];
    }

    /// <summary>
    /// Settings for the file-based outbox (reliability plan, Phase 7), bound from the
    /// <c>"Outbox"</c> section.
    /// </summary>
    public sealed class OutboxOptions
    {
        /// <summary>Configuration section name these options bind from.</summary>
        public const string SectionName = "Outbox";

        /// <summary>
        /// Directory pending outbox entries are written to. Defaults to an <c>Outbox</c> folder
        /// next to the executable when not configured.
        /// </summary>
        public string Directory { get; set; } = string.Empty;
    }

    /// <summary>
    /// The Printer Agent's own standing credential for the background outbox reconciliation job
    /// (Matica Print Flow, reconciliation-credential phase), bound from the
    /// <c>"ReconciliationCredential"</c> section. Exchanged for a short-lived access token via
    /// <c>POST api/auth/service-token</c> once per reconciliation run - never reused from, and
    /// never confused with, any user-delegated Print Agent token.
    /// </summary>
    public sealed class ReconciliationCredentialOptions
    {
        /// <summary>Configuration section name these options bind from.</summary>
        public const string SectionName = "ReconciliationCredential";

        /// <summary>
        /// The service account's public identifier (a GUID, not a secret) - safe to keep in
        /// <c>appsettings.json</c>, matching the Inventory API's own <c>ClientId</c> field.
        /// </summary>
        public string ClientId { get; set; } = string.Empty;

        /// <summary>
        /// The service account's secret. Supplied via user-secrets (development) or an
        /// environment variable (production); never committed to <c>appsettings.json</c>, same
        /// convention as every other secret in this service.
        /// </summary>
        public string ClientSecret { get; set; } = string.Empty;
    }
}
