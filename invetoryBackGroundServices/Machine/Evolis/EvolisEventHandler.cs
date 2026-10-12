using System.Collections.Generic;
using System.Linq;
using Evolis;
using invetoryBackGroundServices.Common;
using invetoryBackGroundServices.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace invetoryBackGroundServices.Machine.Evolis
{
    /// <summary>
    /// How one classified printer event should be handled (Evolis Integration Plan §11). Built
    /// from the real event-name vocabulary — these are exactly the <see cref="Status.Flag"/>
    /// member names, confirmed by reflecting over the installed <c>Evolis.SDK</c> 9.4.1 assembly,
    /// the same source <c>EvolisStatusMapper</c> used for its own §13.3 table.
    /// </summary>
    public enum EvolisEventDisposition
    {
        /// <summary>The operator can fix this (refill the feeder, close the cover, …), but it will not resolve itself — always answered with <c>CANCEL</c>, never retried.</summary>
        OperatorRecoverable,

        /// <summary>May resolve on its own shortly (cooling, waiting for a card) — retried with <c>RETRY</c> up to <see cref="EvolisOptions.MaxEventRetries"/> times before falling back to a specific failure.</summary>
        Transient,

        /// <summary>An event name this catalogue does not recognize — per plan rule §13.4 #7, degrades to a generic error rather than guessing; never retried.</summary>
        Unknown
    }

    /// <summary>
    /// Table-driven, pure classification of one <c>Connection.GetEvent()</c> event name (Evolis
    /// Integration Plan §11) — kept separate from <see cref="EvolisEventHandler"/>'s SDK I/O so it
    /// can be exercised without hardware (plan §15: "event classification is a pure function —
    /// table-driven unit tests over known event names").
    /// </summary>
    public static class EvolisEventClassifier
    {
        /// <summary>How this event name should be answered.</summary>
        public static EvolisEventDisposition Classify(string eventName) => eventName switch
        {
            "WAR_FEEDER_EMPTY" or "ERR_FEEDER_ERROR"
                or "WAR_COVER_OPEN" or "ERR_COVER_OPEN"
                or "WAR_NO_RIBBON" or "WAR_RIBBON_ENDED" or "ERR_RIBBON_ENDED"
                or "WAR_UNSUPPORTED_RIBBON" or "ERR_BAD_RIBBON"
                or "WAR_REJECT_BOX_FULL" or "ERR_REJECT_BOX_FULL"
                or "WAR_HOPPER_FULL" or "ERR_HOPPER_FULL"
                or "ERR_MECHANICAL"
                or "ERR_READ_MAGNETIC" or "ERR_BLANK_TRACK"
                or "ERR_NO_OPTION"
                or "WAR_PRINTER_LOCKED"
                => EvolisEventDisposition.OperatorRecoverable,

            "ERR_HEAD_TEMP" or "WAR_COOLING" or "WAR_WAIT_CARD"
                => EvolisEventDisposition.Transient,

            _ => EvolisEventDisposition.Unknown
        };

        /// <summary>The specific, operator-facing error for this event name — the generic <see cref="EvolisErrors.NeedsAction"/> only for an unrecognized name.</summary>
        public static MachineError ToError(string eventName) => eventName switch
        {
            "WAR_FEEDER_EMPTY" or "ERR_FEEDER_ERROR" => EvolisErrors.FeederEmpty(eventName),
            "WAR_COVER_OPEN" or "ERR_COVER_OPEN" => EvolisErrors.CoverOpen(eventName),
            "WAR_NO_RIBBON" or "WAR_RIBBON_ENDED" or "ERR_RIBBON_ENDED" => EvolisErrors.RibbonEnded(eventName),
            "WAR_UNSUPPORTED_RIBBON" or "ERR_BAD_RIBBON" => EvolisErrors.UnsupportedRibbonFitted(eventName),
            "WAR_REJECT_BOX_FULL" or "ERR_REJECT_BOX_FULL" => EvolisErrors.RejectBoxFull(eventName),
            "WAR_HOPPER_FULL" or "ERR_HOPPER_FULL" => EvolisErrors.HopperFull(eventName),
            "ERR_MECHANICAL" => EvolisErrors.MechanicalFailure(eventName),
            "ERR_HEAD_TEMP" or "WAR_COOLING" => EvolisErrors.Cooling(eventName),
            "WAR_WAIT_CARD" => EvolisErrors.WaitCard(eventName),
            "ERR_READ_MAGNETIC" => EvolisErrors.MagneticError(eventName),
            "ERR_BLANK_TRACK" => EvolisErrors.BlankTrack(eventName),
            "ERR_NO_OPTION" => EvolisErrors.OptionNotFitted(eventName),
            "WAR_PRINTER_LOCKED" => EvolisErrors.PrinterLocked(eventName),
            _ => EvolisErrors.NeedsAction(eventName)
        };
    }

    /// <summary>
    /// Drives the <c>GetEvent</c>/<c>SetEvent</c> loop (Evolis Integration Plan §11) when
    /// <c>Print()</c> returns <c>ENEEDACTION</c>/<c>PRINT_NEEDACTION</c> — called from
    /// <see cref="EvolisPrinterConnection.PrintAsync"/>. Stateless (every retry budget is a local
    /// variable the caller owns, passed by <c>ref</c>, never a field on this class), so it is
    /// registered as a singleton like every other stateless Evolis service in this project.
    /// </summary>
    public sealed class EvolisEventHandler
    {
        private readonly EvolisOptions _options;
        private readonly ILogger<EvolisEventHandler> _logger;

        /// <summary>Creates the handler from its bound options.</summary>
        public EvolisEventHandler(IOptions<EvolisOptions> options, ILogger<EvolisEventHandler> logger)
        {
            _options = options.Value;
            _logger = logger;
        }

        /// <summary>
        /// Answers the printer's currently active event. Returns <c>true</c> when the event was
        /// transient and still within <see cref="EvolisOptions.MaxEventRetries"/> — the caller
        /// should retry the <c>Print()</c> call that produced <c>NEEDACTION</c>, having already
        /// incremented <paramref name="retriesUsed"/>. Returns <c>false</c> with a specific
        /// <paramref name="error"/> otherwise, and the job must fail. Every branch — including an
        /// event name this catalogue does not recognize — calls <c>SetEvent</c> exactly once
        /// before returning, so no event is ever left unanswered (plan rule §13.4 #6).
        /// </summary>
        public bool HandleNeedAction(Connection connection, ref int retriesUsed, out MachineError error)
        {
            (string EventName, List<ServiceAction> Available)? activeEvent = connection.GetEvent();
            if (activeEvent is null)
            {
                // Defensive: Print() reported NEEDACTION but no event is actually active. Nothing
                // to answer with SetEvent here - there is no event name to send it against.
                _logger.LogWarning("Print() returned a NEEDACTION code but GetEvent() reported no active event.");
                error = EvolisErrors.NeedsAction("no active event reported");
                return false;
            }

            (string eventName, List<ServiceAction> available) = activeEvent.Value;
            EvolisEventDisposition disposition = EvolisEventClassifier.Classify(eventName);

            if (disposition == EvolisEventDisposition.Transient
                && retriesUsed < _options.MaxEventRetries
                && available.Contains(ServiceAction.RETRY))
            {
                retriesUsed++;
                if (!connection.SetEvent(eventName, ServiceAction.RETRY))
                {
                    _logger.LogWarning("SetEvent({Event}, RETRY) failed: {LastError}", eventName, connection.GetLastError());
                }

                error = default;
                return true;
            }

            // Every other case - operator-recoverable, an exhausted transient retry budget, or an
            // unrecognized name - is answered with CANCEL, falling back to whatever this
            // particular event does accept if CANCEL itself is not offered. Never left unanswered.
            ServiceAction reply = available.Contains(ServiceAction.CANCEL) ? ServiceAction.CANCEL
                : available.Contains(ServiceAction.OK) ? ServiceAction.OK
                : available.FirstOrDefault();

            if (!connection.SetEvent(eventName, reply))
            {
                _logger.LogWarning("SetEvent({Event}, {Action}) failed: {LastError}", eventName, reply, connection.GetLastError());
            }

            if (disposition == EvolisEventDisposition.Unknown)
            {
                _logger.LogWarning(
                    "Unclassified Evolis event '{EventName}' (available actions: {Actions}) - answered with {Reply} and reported as a generic NeedsAction error.",
                    eventName, string.Join(",", available), reply);
            }

            error = EvolisEventClassifier.ToError(eventName);
            return false;
        }
    }
}
