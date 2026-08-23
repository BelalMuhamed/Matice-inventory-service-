using System;
using System.Threading;
using System.Threading.Tasks;
using invetoryBackGroundServices.PrintFlow;
using invetoryBackGroundServices.Services;
using MATICA_S3300e.LAN;

namespace invetoryBackGroundServices.Outbox
{
    /// <summary>
    /// Reconciles pending <see cref="OutboxEntry"/> records - print outcomes whose Backend Call #2
    /// confirmation to the Inventory API could not be delivered even after the in-request retry
    /// loop was exhausted. Run both on a recurring schedule (every 30 minutes, see
    /// <c>Program.cs</c>) and once at startup, so a crash between the original attempt and the
    /// next scheduled tick doesn't leave anything stranded until the following tick.
    /// <para>
    /// <b>Known, unresolved limitation - flagged rather than built around silently:</b> each entry
    /// carries the Print Agent token from its <em>original</em> print attempt
    /// (<see cref="OutboxEntry.BearerToken"/>), and that token is short-lived (5 minutes by
    /// default on the Inventory API side) specifically so a compromised branch machine's blast
    /// radius stays small. This job's whole reason to exist is reconciling failures that survive
    /// past a single request's retry window - by the time a 30-minute scheduled sweep runs, the
    /// original token will essentially always have expired, and the Printer Agent has no way to
    /// mint itself a fresh one (only Angular, with the user's live session, can call
    /// <c>POST api/auth/print-agent-token</c>). The startup scan has a real chance of succeeding
    /// (a quick crash-and-restart can land within the token's 5-minute window), but the scheduled
    /// 30-minute sweep will typically just confirm the token is dead, log it, and leave the entry
    /// queued - which is honest behavior, not a bug, but it means this job alone does not fully
    /// close the reliability gap it was built for. Closing it needs one of: a separate, narrowly-
    /// scoped, longer-lived credential used only by this job (a real secret-management decision,
    /// not something to pick silently); a way for this service to request a fresh token itself
    /// (a new Inventory API capability); or accepting that anything still unconfirmed after the
    /// token expires becomes a manual-reconciliation item, with the outbox directory itself as
    /// the audit trail. That decision is outstanding.
    /// </para>
    /// </summary>
    public sealed class OutboxReconciliationJob
    {
        private readonly IOutboxStore _store;
        private readonly IPrintFlowClient _printFlowClient;
        private readonly Logger _log;

        /// <summary>Creates the job with its collaborators.</summary>
        public OutboxReconciliationJob(IOutboxStore store, IPrintFlowClient printFlowClient, Logger log)
        {
            _store = store;
            _printFlowClient = printFlowClient;
            _log = log;
        }

        /// <summary>Attempts to reconcile every pending entry once.</summary>
        public async Task RunAsync(CancellationToken cancellationToken = default)
        {
            var entries = await _store.GetAllAsync();
            if (entries.Count == 0)
            {
                return;
            }

            _log.AppendLog($"Outbox sweep: {entries.Count} pending entr{(entries.Count == 1 ? "y" : "ies")}.", Logger.LogType.Info);

            foreach (OutboxEntry entry in entries)
            {
                await ReconcileOneAsync(entry, cancellationToken);
            }
        }

        private async Task ReconcileOneAsync(OutboxEntry entry, CancellationToken cancellationToken)
        {
            PrintFlowResult<object?> result = await _printFlowClient.RecordPrintResultAsync(
                entry.BearerToken, entry.ProductItemId, entry.BranchId, entry.Success, entry.HolderName,
                entry.IdempotencyKey, cancellationToken);

            if (result.Success)
            {
                await _store.DeleteAsync(entry.IdempotencyKey);
                _log.AppendLog(
                    $"Outbox: reconciled item {entry.ProductItemId}, idempotencyKey {entry.IdempotencyKey}.",
                    Logger.LogType.Info);
                return;
            }

            // Still not confirmed - almost certainly the token-expiry limitation described above
            // for anything past the startup scan. Recorded on the entry itself (attempt count,
            // last failure) so the outbox directory doubles as an audit trail for manual
            // reconciliation, then left in place for the next sweep rather than discarded.
            entry.AttemptCount++;
            entry.LastAttemptUtc = DateTime.UtcNow;
            entry.LastFailureReason = result.ErrorMessage;
            await _store.UpdateAsync(entry);

            _log.AppendLog(
                $"Outbox: reconciliation attempt {entry.AttemptCount} for item {entry.ProductItemId}, " +
                $"idempotencyKey {entry.IdempotencyKey} still failing: {result.ErrorMessage}",
                Logger.LogType.Error);
        }
    }
}
