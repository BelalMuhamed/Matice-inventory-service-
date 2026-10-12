namespace invetoryBackGroundServices.Options
{
    /// <summary>
    /// Settings for the Evolis SDK integration (Evolis Integration Plan §8), bound from the
    /// <c>"Evolis"</c> section. Backend configuration, not request data — same reasoning as
    /// <see cref="MachineCommunicationOptions"/>.
    /// </summary>
    public sealed class EvolisOptions
    {
        /// <summary>Configuration section name these options bind from.</summary>
        public const string SectionName = "Evolis";

        /// <summary>
        /// SDK <c>Connection</c> open mode (decision Q-29). Default <c>DIRECT</c> — confirmed live
        /// against a connected Evolis Primacy 2 (Phase 0-equivalent probe during this phase).
        /// Valid values: <c>AUTO</c>, <c>DIRECT</c>, <c>SUPERVISED</c>.
        /// </summary>
        public string OpenMode { get; set; } = "DIRECT";

        /// <summary>SDK error-management mode (decision Q-29). Valid values: <c>PRINTER</c>, <c>SOFTWARE</c>.</summary>
        public string ErrorManagement { get; set; } = "SOFTWARE";

        /// <summary>
        /// Whether a print job calls <c>Connection.Reserve()</c> before working the printer.
        /// Fallback switch to <c>false</c> if a future supervised deployment returns
        /// <c>SESSION_EBUSY</c> on every reservation attempt.
        /// </summary>
        public bool ReserveOnJob { get; set; } = true;

        /// <summary>Per-command timeout in milliseconds. Matches <c>Evolis.Evolis.TIMEOUT</c> (30s).</summary>
        public int CommandTimeoutMs { get; set; } = 30000;

        /// <summary>Magnetic-track read timeout in milliseconds.</summary>
        public int MagReadTimeoutMs { get; set; } = 30000;

        /// <summary>Print job timeout in milliseconds. Generous — a duplex YMCKO job on a Primacy 2 is slow.</summary>
        public int PrintTimeoutMs { get; set; } = 120000;

        /// <summary>Whether <see cref="Services.EvolisSupervisorRegistrar"/> runs discovery and registers at startup (decision Q-21).</summary>
        public bool AutoRegisterSupervisorOnStartup { get; set; } = true;

        /// <summary>When non-empty, passed to <c>Evolis.SetLogPath</c> at startup to capture a native SDK trace.</summary>
        public string NativeLogPath { get; set; } = string.Empty;

        /// <summary>Native SDK log level, passed to <c>Evolis.SetLogLevel</c> at startup. Valid values match <c>Evolis.LogLevel</c>.</summary>
        public string NativeLogLevel { get; set; } = "Warning";

        /// <summary>
        /// Font family substituted by <c>SkiaCardRenderer</c> (Evolis Integration Plan §9) when a
        /// product's configured <c>FontFamily</c> is not installed on this host. A warning is
        /// logged and returned in <c>CardRenderResult.Warnings</c> whenever this fallback fires —
        /// never silent.
        /// </summary>
        public string DefaultFontFamily { get; set; } = "Arial";

        /// <summary>
        /// Panel-selection threshold (decision Q-25): a <c>PrintColor</c> whose R, G, and B
        /// components are all less than or equal to this value is treated as monochrome black
        /// (<c>CardPanel.ResinBlack</c>, printed via <c>PrintSession.SetBlack</c>); anything else
        /// is <c>CardPanel.Colour</c> (printed via <c>PrintSession.SetImage</c>, YMC panels only).
        /// Doubles as the monochrome 1-bpp encode threshold in <c>SkiaCardRenderer</c> (same value,
        /// no separate per-request override — see CLAUDE.md's Phase 5 follow-up).
        /// </summary>
        public byte MonochromeThreshold { get; set; } = 32;

        /// <summary>
        /// Fixed resin-panel darkness applied to <c>SettingsKey.BMonochromeContrast</c>/
        /// <c>FMonochromeContrast</c> (SDK-confirmed range 1-20) on every print whose panel is
        /// <c>CardPanel.ResinBlack</c>. Settled by live tuning against the real connected Primacy 2 —
        /// 20 (the max) was the best result found; the developer confirmed this applies uniformly to
        /// every product, no per-card override needed. See CLAUDE.md's Phase 5 follow-up.
        /// </summary>
        public int MonochromeContrast { get; set; } = 20;

        /// <summary>
        /// Fixed value applied to <c>SettingsKey.IGMonochromeSpeed</c> (global, not per-face,
        /// SDK-confirmed range 1-10) on every print whose panel is <c>CardPanel.ResinBlack</c>. A
        /// slower speed gives the thermal head more dwell time per pixel, transferring more resin
        /// (darker) — confirmed live. Settled at 1 (the slowest) by the same tuning session as
        /// <see cref="MonochromeContrast"/>, uniform across every product.
        /// </summary>
        public int MonochromeSpeed { get; set; } = 1;

        /// <summary>
        /// Bounded retry budget (Evolis Integration Plan §11) for a <em>transient</em> printer
        /// event (<c>ERR_HEAD_TEMP</c>/<c>WAR_COOLING</c>/<c>WAR_WAIT_CARD</c>) surfaced via
        /// <c>Connection.GetEvent()</c> after <c>Print()</c> returns <c>ENEEDACTION</c>/
        /// <c>PRINT_NEEDACTION</c> — see <see cref="Machine.Evolis.EvolisEventHandler"/>. Once this budget is
        /// exhausted the job fails with a specific error instead of retrying forever; a mechanical
        /// or operator-actionable event (feeder empty, cover open, …) never retries regardless of
        /// this setting.
        /// </summary>
        public int MaxEventRetries { get; set; } = 1;
    }
}
