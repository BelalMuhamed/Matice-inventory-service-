using System;
using System.Threading;
using System.Threading.Tasks;
using Evolis;
using invetoryBackGroundServices.Common;
using invetoryBackGroundServices.Machine.Abstractions;
using invetoryBackGroundServices.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace invetoryBackGroundServices.Machine.Evolis
{
    /// <summary>
    /// <see cref="ICardPrinterConnection"/> for Evolis (Evolis Integration Plan §10, steps 3-10) —
    /// the only place <c>Evolis.MagSession</c>/<c>Evolis.PrintSession</c> are touched for the
    /// print flow. Every blocking SDK call runs via <see cref="Task.Run"/>, same pattern as
    /// <see cref="EvolisDeviceDiscovery"/> and <c>EvolisMachineController</c>.
    /// <para>
    /// Real SDK findings confirmed live while building this (see CLAUDE.md's Phase 5 section):
    /// trays go through the typed <c>Connection.SetInputTray</c>/<c>SetOutputTray</c>/
    /// <c>SetErrorTray</c> calls, not <c>SetSetting(GInputTray/GOutputTray, ...)</c> (a different,
    /// narrower string vocabulary that doesn't even cover this deployment's own configured
    /// default output tray); the duplex per-job toggle is <c>SettingsKey.Duplex</c>
    /// (<c>NONE</c>/<c>HORIZONTAL</c>), not <c>GDuplexType</c> (a separate key describing which
    /// physical duplex module is installed, with no <c>EvolisPrinterConfiguration</c> column at
    /// all); and <c>GRibbonType</c>'s string vocabulary is translated from
    /// <c>RibbonTypes.SdkCode</c> by <see cref="EvolisPrintSettingsMapper"/> before it ever
    /// reaches this class.
    /// </para>
    /// </summary>
    public sealed class EvolisPrinterConnection : ICardPrinterConnection
    {
        private readonly EvolisConnectionScope _scope;
        private readonly EvolisOptions _options;
        private readonly ILogger _logger;
        private readonly EvolisEventHandler _eventHandler;

        internal EvolisPrinterConnection(EvolisConnectionScope scope, EvolisOptions options, ILogger logger, EvolisEventHandler eventHandler)
        {
            _scope = scope;
            _options = options;
            _logger = logger;
            _eventHandler = eventHandler;
        }

        /// <inheritdoc />
        public bool IsReserved => _scope.IsReserved;

        /// <summary>
        /// Applies the job's tray and coercivity configuration — called once by
        /// <see cref="EvolisPrinterAdapter.OpenAsync"/> right after the connection opens, not
        /// exposed on the public interface (it's part of opening, not a per-step operation).
        /// </summary>
        internal void ApplyConfiguration(Enums.InputTray inputTray, Enums.OutputTray outputTray, Enums.OutputTray errorTray, Enums.MagCoercivity magCoercivity)
        {
            Connection connection = _scope.Connection;

            if (!connection.SetInputTray(ParseByName<InputTray>(inputTray)))
            {
                _logger.LogWarning("SetInputTray({Tray}) failed: {LastError}", inputTray, connection.GetLastError());
            }

            if (!connection.SetOutputTray(ParseByName<OutputTray>(outputTray)))
            {
                _logger.LogWarning("SetOutputTray({Tray}) failed: {LastError}", outputTray, connection.GetLastError());
            }

            if (!connection.SetErrorTray(ParseByName<OutputTray>(errorTray)))
            {
                _logger.LogWarning("SetErrorTray({Tray}) failed: {LastError}", errorTray, connection.GetLastError());
            }

            Connection localConnection = connection;
            var mag = new MagSession(ref localConnection);
            mag.SetCoercivity(ParseByName<MagCoercivity>(magCoercivity));
        }

        /// <inheritdoc />
        public Task<MagReadResult> ReadTrack2Async(CancellationToken cancellationToken = default) =>
            Task.Run(() =>
            {
                Connection connection = _scope.Connection;
                var mag = new MagSession(ref connection);
                mag.Init();

                ReturnCode rc = mag.ReadTracks(false, true, false, _options.MagReadTimeoutMs);
                switch (rc)
                {
                    case ReturnCode.OK:
                        string track2 = mag.GetData(MagTrackNumber.TRACK2);
                        return new MagReadResult(MagReadOutcome.Success, track2);

                    case ReturnCode.MAG_EBLANK:
                        return new MagReadResult(MagReadOutcome.Blank, null, rc.ToString());

                    case ReturnCode.MAG_EDATA:
                    case ReturnCode.MAG_ERROR:
                        return new MagReadResult(MagReadOutcome.Unreadable, null, rc.ToString());

                    default:
                        _logger.LogWarning("ReadTracks failed: {ReturnCode}", rc);
                        return new MagReadResult(MagReadOutcome.Failed, null, rc.ToString());
                }
            }, cancellationToken);

        /// <inheritdoc />
        public Task<string?> GetLoadedRibbonSdkCodeAsync(CancellationToken cancellationToken = default) =>
            Task.Run(() =>
            {
                Connection connection = _scope.Connection;
                if (connection.GetRibbonInfo(out RibbonInfo ribbonInfo))
                {
                    return (string?)ribbonInfo.type.ToString();
                }

                _logger.LogWarning("GetRibbonInfo failed: {LastError}", connection.GetLastError());
                return (string?)null;
            }, cancellationToken);

        /// <inheritdoc />
        public Task<CardPrintOutcome> PrintAsync(CardPrintRequest request, CancellationToken cancellationToken = default) =>
            Task.Run(() =>
            {
                Connection connection = _scope.Connection;
                var print = new PrintSession(ref connection);

                if (!print.Init())
                {
                    string detail = print.GetLastError().ToString();
                    _logger.LogWarning("PrintSession.Init() failed: {LastError}", detail);
                    return new CardPrintOutcome(false, detail);
                }

                if (!print.SetSetting(SettingsKey.GRibbonType, request.RibbonSettingsToken))
                {
                    string detail = $"SetSetting(GRibbonType, '{request.RibbonSettingsToken}') failed: {print.GetLastError()}";
                    _logger.LogWarning(detail);
                    return new CardPrintOutcome(false, detail);
                }

                // Resin-panel darkness (SDK-confirmed range 1-20) - only meaningful for the
                // monochrome/resin panel, and best-effort: a failed override still prints at
                // whatever contrast the driver already has, it just doesn't get the requested
                // boost. The SDK has a separate key per face - BMonochromeContrast for the back,
                // FMonochromeContrast for the front - and setting the wrong one for the face being
                // printed has no visual effect (confirmed live: a back-side print came out faint
                // with only FMonochromeContrast set). So the key is chosen from PrintedFace, not
                // hardcoded to Front.
                //
                // Fixed, not per-request: settled by live tuning against the real Primacy 2
                // (contrast 20, speed 1) and confirmed by the developer to apply uniformly to every
                // product - see EvolisOptions.MonochromeContrast/MonochromeSpeed and CLAUDE.md's
                // Phase 5 follow-up. DualDeposite was removed outright (confirmed IsSettingValid=false
                // on this printer+ribbon - no effect on this hardware).
                bool isBack = request.PrintedFace == Enums.PrintedFace.Back;

                if (request.Panel == Rendering.CardPanel.ResinBlack)
                {
                    SettingsKey contrastKey = isBack ? SettingsKey.BMonochromeContrast : SettingsKey.FMonochromeContrast;
                    if (!print.SetIntSetting(contrastKey, _options.MonochromeContrast))
                    {
                        _logger.LogWarning("SetIntSetting({Key}, {Value}) failed: {LastError}", contrastKey, _options.MonochromeContrast, print.GetLastError());
                    }

                    if (!print.SetIntSetting(SettingsKey.IGMonochromeSpeed, _options.MonochromeSpeed))
                    {
                        _logger.LogWarning("SetIntSetting(IGMonochromeSpeed, {Value}) failed: {LastError}", _options.MonochromeSpeed, print.GetLastError());
                    }
                }

                string duplexValue = request.PrintedFace == Enums.PrintedFace.Back ? "HORIZONTAL" : "NONE";
                if (!print.SetSetting(SettingsKey.Duplex, duplexValue))
                {
                    _logger.LogWarning("SetSetting(Duplex, '{Value}') failed: {LastError}", duplexValue, print.GetLastError());
                }

                // Orchestrator controls eject/reject itself (step 10) - a spoiled card must go to
                // the error tray, never the output hopper, so auto-eject is deliberately off.
                print.SetAutoEject(false);

                CardFace face = request.PrintedFace == Enums.PrintedFace.Back ? CardFace.BACK : CardFace.FRONT;
                bool imageSet = request.Panel == Rendering.CardPanel.ResinBlack
                    ? print.SetBlack(face, request.BmpBytes)
                    : print.SetImage(face, request.BmpBytes);

                if (!imageSet)
                {
                    string detail = print.GetLastError().ToString();
                    _logger.LogWarning("Setting the print image failed: {LastError}", detail);
                    return new CardPrintOutcome(false, detail);
                }

                // Evolis Integration Plan §11's error-event loop: Print() can return NEEDACTION
                // mid-job (feeder emptied, cover opened, cooling, ...) with an active event only
                // Connection.GetEvent()/SetEvent() can answer. A transient event (cooling, waiting
                // for a card) is retried in place, bounded by MaxEventRetries; every other event -
                // operator-recoverable or unrecognized - fails the job with a specific error
                // instead of the bare ReturnCode, and is never left unanswered.
                int eventRetriesUsed = 0;
                while (true)
                {
                    ReturnCode printResult = print.Print(_options.PrintTimeoutMs);
                    if (printResult == ReturnCode.OK)
                    {
                        return new CardPrintOutcome(true);
                    }

                    if (printResult is not (ReturnCode.ENEEDACTION or ReturnCode.PRINT_NEEDACTION))
                    {
                        return new CardPrintOutcome(false, printResult.ToString());
                    }

                    bool retry = _eventHandler.HandleNeedAction(connection, ref eventRetriesUsed, out MachineError eventError);
                    if (retry)
                    {
                        continue;
                    }

                    return new CardPrintOutcome(false, eventError.Message, eventError);
                }
            }, cancellationToken);

        /// <inheritdoc />
        public Task<bool> EjectAsync(CancellationToken cancellationToken = default) =>
            Task.Run(() =>
            {
                bool ok = _scope.Connection.EjectCard();
                if (!ok)
                {
                    _logger.LogWarning("EjectCard() failed: {LastError}", _scope.Connection.GetLastError());
                }

                return ok;
            }, cancellationToken);

        /// <inheritdoc />
        public Task<bool> RejectAsync(CancellationToken cancellationToken = default) =>
            Task.Run(() =>
            {
                bool ok = _scope.Connection.RejectCard();
                if (!ok)
                {
                    _logger.LogWarning("RejectCard() failed: {LastError}", _scope.Connection.GetLastError());
                }

                return ok;
            }, cancellationToken);

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            _scope.Dispose();
            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// Translates between this service's mirror enum and the real SDK enum of the identical
        /// name — safe only because every member name was individually confirmed to match
        /// (<see cref="EvolisPrintSettingsMapper"/>'s remarks); never used for a type pair that
        /// hasn't been.
        /// </summary>
        private static TSdkEnum ParseByName<TSdkEnum>(Enum agentValue) where TSdkEnum : struct, Enum =>
            Enum.Parse<TSdkEnum>(agentValue.ToString());
    }
}
