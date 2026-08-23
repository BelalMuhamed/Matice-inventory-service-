using System;
using System.IO;
using System.Security.Claims;
using System.Threading;
using AUBServicesLayer.Enums;
using invetoryBackGroundServices.Common;
using invetoryBackGroundServices.Machine;
using invetoryBackGroundServices.Params;
using invetoryBackGroundServices.PrintFlow;
using invetoryBackGroundServices.Security;
using invetoryBackGroundServices.Services;
using MATICA_S3300e.CLS;
using MATICA_S3300e.LAN;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
namespace invetoryBackGroundServices.Controllers
{
    /// <summary>
    /// Matica Print Flow: every action here now requires a valid Print Agent token (short-lived,
    /// minted by the Inventory API, never the caller's real session token) - see
    /// <see cref="Program"/> for the JWT bearer scheme this validates against and
    /// <see cref="PrintAgentAuthPolicy"/> for the policy. This service still owns nothing about
    /// business rules: <see cref="Print"/> calls the Inventory API's two print-flow endpoints and
    /// reacts to what they say, it doesn't decide Known-way/Unknown-way validation itself.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = PrintAgentAuthPolicy.Name)]
    public class MachineController : ControllerBase
    {
        private readonly MachineConnectionClass ConnectionInfo;
        private readonly MachineInfoJSON MachineInfo;
        private readonly ActionClass httpAction;
        private readonly CardData Data;
        private readonly IPrintFlowClient _printFlowClient;
        private readonly IMaticaCommandClient _machine;
        private readonly Logger _log;

        string ERROR = string.Empty;
        string InfoText = string.Empty;
        MachineCommands MachineComm;

        /// <summary>
        /// <paramref name="log"/> is now injected (singleton, see <see cref="Program"/>) instead of
        /// being constructed here per request - the old per-request construction meant every
        /// request got its own <see cref="System.Threading.Mutex"/> that never actually serialized
        /// writes against any other request's Mutex, and re-ran the log-directory scan-and-delete
        /// on every single call instead of once at startup (prior review, Critical finding).
        /// <para>
        /// <paramref name="machine"/> is the new async command client. The legacy
        /// <see cref="MachineCommands"/> instance is still constructed here because
        /// <see cref="Print"/> continues to use it until the print flow migrates in the next
        /// phase - the two coexist deliberately rather than the print path being changed twice.
        /// </para>
        /// </summary>
        public MachineController(
            MachineConnectionClass connectionInfo, MachineInfoJSON machineInfo, ActionClass HttpAction,
            CardData data, IPrintFlowClient printFlowClient, IMaticaCommandClient machine, Logger log)
        {
            this.ConnectionInfo = connectionInfo;
            this.MachineInfo = machineInfo;
            this.httpAction = HttpAction;
            this.Data = data;
            _printFlowClient = printFlowClient;
            _machine = machine;
            _log = log;
            MachineComm = new MachineCommands(HttpAction, connectionInfo, machineInfo, data, _log);
        }

        /// <summary>Reads the raw bearer token from this request's own Authorization header, to forward unchanged to the Inventory API.</summary>
        private string GetBearerToken()
        {
            string header = Request.Headers.Authorization.ToString();
            const string prefix = "Bearer ";
            return header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? header[prefix.Length..] : header;
        }

        /// <summary>
        /// Defense in depth: the Print Agent token already scopes its holder to one branch via its
        /// own claim, set once at mint time by the Inventory API after validating tenant ownership.
        /// This just confirms the request body cannot silently disagree with the token's own claim.
        /// </summary>
        private bool IsOutsideTokenScope(long requestedBranchId) =>
            User.FindFirstValue(PrintAgentClaims.BranchId) != requestedBranchId.ToString();

        /// <summary>
        /// Reads machine status. Returns the parsed status object inside the standard envelope
        /// rather than the machine's raw JSON text, which is what the previous version returned
        /// as a bare "message" string.
        /// </summary>
        [HttpPost("get-machine-info")]
        [ProducesResponseType(typeof(ApiResponse<MachineResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMachineInfo(
            GetMachineInfoReques PrintRequest, CancellationToken cancellationToken)
        {
            IActionResult? invalid = ValidateConnection(PrintRequest.Ip, PrintRequest.Port, out string ip);
            if (invalid is not null) return invalid;

            MachineResponse info = await _machine.GetInfoAsync(ip, PrintRequest.Port, cancellationToken);
            return Ok(ApiResponse<MachineResponse>.Ok(info));
        }

        /// <summary>Issues the Restore (reset) command.</summary>
        [HttpPost("reset-machine")]
        [ProducesResponseType(typeof(ApiResponse<MachineOperationResult>), StatusCodes.Status200OK)]
        public async Task<IActionResult> ResetMachine(
            GetMachineInfoReques PrintRequest, CancellationToken cancellationToken)
        {
            IActionResult? invalid = ValidateConnection(PrintRequest.Ip, PrintRequest.Port, out string ip);
            if (invalid is not null) return invalid;

            await _machine.RestoreAsync(ip, PrintRequest.Port, cancellationToken);
            return Ok(ApiResponse<MachineOperationResult>.Ok(new MachineOperationResult("Restore")));
        }

        /// <summary>Ejects the currently loaded card to the supplied hopper.</summary>
        [HttpPost("Eject-card")]
        [ProducesResponseType(typeof(ApiResponse<MachineOperationResult>), StatusCodes.Status200OK)]
        public async Task<IActionResult> EjectCard(EjectCardReq Dto, CancellationToken cancellationToken)
        {
            IActionResult? invalid = ValidateConnection(Dto.Ip, Dto.Port, out string ip);
            if (invalid is not null) return invalid;

            await _machine.EjectCardAsync(ip, Dto.Port, Dto.HooperId, cancellationToken);
            return Ok(ApiResponse<MachineOperationResult>.Ok(new MachineOperationResult("EjectCard")));
        }

        /// <summary>
        /// Validates and normalizes the connection details common to every machine endpoint.
        /// Returns null when valid; otherwise the failure response to return directly.
        /// <para>
        /// Note the behavior change: the previous code passed the caller's IP straight through
        /// <c>UTILITIES.FormatIP</c> and ignored the error it reported, so a malformed IP produced
        /// a confusing downstream connection failure instead of a clear validation error.
        /// </para>
        /// </summary>
        private IActionResult? ValidateConnection(string? rawIp, string? port, out string ip)
        {
            ip = string.Empty;

            if (string.IsNullOrWhiteSpace(rawIp))
            {
                return Failure(MachineErrors.InvalidIp());
            }

            string formatted = UTILITIES.FormatIP(out string formatError, rawIp);
            if (!string.IsNullOrWhiteSpace(formatError) || string.IsNullOrWhiteSpace(formatted))
            {
                return Failure(MachineErrors.InvalidIp());
            }

            if (string.IsNullOrWhiteSpace(port) || !int.TryParse(port, out int parsedPort)
                || parsedPort <= 0 || parsedPort > 65535)
            {
                return Failure(MachineErrors.InvalidPort());
            }

            ip = formatted;
            return null;
        }

        /// <summary>Builds a failure response in the standard envelope with the mapped status code.</summary>
        private IActionResult Failure(MachineError error) =>
            StatusCode(error.StatusCode, ApiResponse<object>.Fail(error.ToApiError()));





        /// <summary>
        /// Matica Print Flow, migrated to the async command client. Steps and their order are
        /// unchanged from the previous version - ready check, LoadCard, ReadMAG, Backend Call #1,
        /// Emboss, local batch write, EjectCard, Backend Call #2 - only the transport underneath
        /// each step changed. This service still owns no business logic: it calls the Inventory
        /// API's two print-flow endpoints and reacts to what they say, it doesn't decide
        /// Known-way/Unknown-way validation itself.
        /// <para>
        /// Failure handling changed shape, not behavior: the async client throws typed
        /// <see cref="MachineException"/>s instead of returning a reply code, so steps whose
        /// failure should immediately end the request (the ready check, LoadCard) simply let the
        /// exception propagate to <see cref="Middleware.GlobalExceptionMiddleware"/>. Steps that
        /// need a specific recovery action first - ReadMAG and Backend Call #1 both eject the card
        /// before reporting failure, exactly as before - catch locally and eject, preserving the
        /// original's own quirk of reporting the eject failure instead of the original error when
        /// eject itself also fails. Emboss and the final EjectCard are caught locally too, since
        /// the flow must continue to the local batch write and Backend Call #2 regardless of
        /// whether the physical print succeeded - this mirrors the previous
        /// <c>bool printSucceeded = !MachineComm.ValuateReply(Reply)</c> pattern exactly, just
        /// sourced from a caught exception instead of a reply code.
        /// </para>
        /// </summary>
        [HttpPost("Print-Card-Holder-Name")]
        public async Task<IActionResult> Print([FromBody] PrintReqDto dto, CancellationToken cancellationToken)
        {
            if (IsOutsideTokenScope(dto.BranchId))
            {
                return Forbid();
            }

            IActionResult? invalid = ValidateConnection(dto.MachineIp, dto.Port, out string ip);
            if (invalid is not null) return invalid;

            // Generated once per physical print attempt, reused on every retry of Backend Call #2
            // (see PrintFlowClient.RecordPrintResultAsync) - never regenerated mid-attempt.
            string idempotencyKey = Guid.NewGuid().ToString();

            #region get machine info and check status
            // GetInfoAsync throwing here (communication failure, timeout, protocol error)
            // propagates to the exception middleware directly - nothing to eject yet, so there is
            // no recovery action to perform first, same as the previous version's behavior.
            MachineResponse status = await _machine.GetInfoAsync(ip, dto.Port, cancellationToken);

            if (status.MachineStatus is null
                || status.MachineStatus.machineStatus != "READY"
                || status.MachineStatus.CardInside != "no"
                || status.MachineStatus.TipperStatus != "Ready")
            {
                return Failure(MachineErrors.NotReady());
            }
            #endregion

            #region loadcard
            // Same reasoning as the ready check: a LoadCard failure propagates directly, since
            // there is no card to eject yet if loading itself failed.
            await _machine.LoadCardAsync(ip, dto.Port, dto.FeederId, cancellationToken);
            #endregion

            #region ReadMAGData
            string trackData;
            try
            {
                trackData = await _machine.ReadMagAsync(ip, dto.Port, "2", cancellationToken);
            }
            catch (MachineException ex)
            {
                return await EjectThenFailAsync(ip, dto.Port, dto.HopperId, ex.Error, cancellationToken);
            }

            // Raw PAN, used only transiently for Backend Call #1 below - never logged, never
            // assigned anywhere else, never persisted locally. Matica Print Flow, raw PAN handling.
            string rawPan = trackData.Length >= 16 ? trackData.Substring(0, 16) : trackData;
            #endregion

            #region Backend Call #1: resolve-for-print
            PrintFlowResult<ResolveForPrintResponseDto> resolveResult = await _printFlowClient.ResolveForPrintAsync(
                GetBearerToken(), rawPan, dto.ProductId, dto.BranchId, cancellationToken);
            // rawPan is not referenced again after this call.

            if (!resolveResult.Success)
            {
                _log.AppendLog(
                    " [Card Holder Name:" + dto.CardHolderName + "] [Product:" + dto.ProductId +
                    "] resolve-for-print failed: " + resolveResult.ErrorMessage, Logger.LogType.Error);

                MachineError backendError = resolveResult.IsTransient
                    ? MachineErrors.BackendUnavailable(resolveResult.ErrorMessage)
                    : MachineErrors.BackendRejected(resolveResult.ErrorMessage);

                return await EjectThenFailAsync(ip, dto.Port, dto.HopperId, backendError, cancellationToken);
            }

            long productItemId = resolveResult.Data!.ProductItemId;
            string maskedPan = resolveResult.Data.MaskedPan;
            #endregion

            #region EmbossCardHolderName
            var embossRequest = new EmbossRequest(
                dto.CardHolderName.Trim(), dto.Font, dto.Cpi, dto.OffsetX, dto.OffsetY,
                dto.TipperTemperature, dto.TipperPressure, dto.TipperConsumption, dto.TipperTime);

            bool printSucceeded;
            try
            {
                await _machine.EmbossAsync(ip, dto.Port, embossRequest, cancellationToken);
                printSucceeded = true;
            }
            catch (MachineException ex)
            {
                printSucceeded = false;
                _log.AppendLog($"Emboss failed: {ex.Message}", Logger.LogType.Error);
            }
            #endregion

            #region save to local batch
            string logData = " [Card Holder Name:" + dto.CardHolderName + "] [Product:" + dto.ProductId +
                "] [Card PAN:" + maskedPan + "] [Status:" + (printSucceeded ? "Success" : "Error") + "]";
            _log.AppendLog(logData, printSucceeded ? Logger.LogType.Info : Logger.LogType.Error);

            string localBatch = Path.Combine(AppContext.BaseDirectory, "LocalBatch.lbt");
            if (!System.IO.File.Exists(localBatch)) System.IO.File.Create(localBatch).Close();
            using (StreamWriter streamWriter = new StreamWriter(localBatch, true))
            {
                string lineValue = $"{maskedPan}|" +
                    $"{dto.CardHolderName.Trim()}|" +
                    $"{dto.BranchId}|" +
                    $"{dto.UserName}|{(printSucceeded ? 1 : 3)}|" +
                    $"{(printSucceeded ? "Print Card Success" : "Error in Print Card")}|" +
                    $"{dto.ProductId}";
                string lineValueCipher = ENCRYPTION.Enc_TripleDES(out ERROR, lineValue, GLOBALS._KEY_CONFIG);
                streamWriter.WriteLine(lineValueCipher);
                streamWriter.Close();
            }
            #endregion

            #region EjectCard
            // Eject before Backend Call #2, deliberately: the physical card is already
            // printed-or-spoiled by this point, so it goes to the operator/customer promptly
            // instead of sitting inside the machine for the duration of Backend Call #2's retry
            // loop (up to a few seconds on a transient failure).
            bool ejectSucceeded;
            try
            {
                await _machine.EjectCardAsync(ip, dto.Port, dto.HopperId, cancellationToken);
                ejectSucceeded = true;
            }
            catch (MachineException ex)
            {
                ejectSucceeded = false;
                _log.AppendLog($"Eject failed: {ex.Message}", Logger.LogType.Error);
            }
            #endregion

            #region Backend Call #2: print-result
            PrintFlowResult<object?> recordResult = await _printFlowClient.RecordPrintResultAsync(
                GetBearerToken(), productItemId, dto.BranchId, printSucceeded, dto.CardHolderName.Trim(),
                idempotencyKey, cancellationToken);
            #endregion

            if (!ejectSucceeded)
            {
                return Failure(MachineErrors.CannotEject());
            }

            if (!printSucceeded)
            {
                return Failure(MachineErrors.Rejected("Error printing card."));
            }

            if (!recordResult.Success)
            {
                // The physical print already succeeded and the card has been ejected - this is the
                // "printer succeeds, backend logging fails" scenario from the plan. Told to the
                // caller distinctly (still Success: true - the card is fine) rather than as an
                // ordinary failure, since only the Inventory API's record of it could not be
                // confirmed.
                _log.AppendLog(
                    $"print-result could not be confirmed for item {productItemId}, idempotencyKey {idempotencyKey}: " +
                    recordResult.ErrorMessage, Logger.LogType.Error);

                return StatusCode(207, ApiResponse<PrintResult>.Ok(
                    new PrintResult(true, false, productItemId, idempotencyKey)));
            }

            return Ok(ApiResponse<PrintResult>.Ok(new PrintResult(true, true, productItemId, idempotencyKey)));
        }

        /// <summary>
        /// Attempts to eject the card before reporting <paramref name="originalError"/>, matching
        /// the pre-migration behavior exactly - including its own quirk: if the eject attempt
        /// itself also fails, the eject failure is reported instead of the original error, not
        /// alongside it.
        /// </summary>
        private async Task<IActionResult> EjectThenFailAsync(
            string ip, string port, int hopperId, MachineError originalError, CancellationToken cancellationToken)
        {
            try
            {
                await _machine.EjectCardAsync(ip, port, hopperId, cancellationToken);
            }
            catch (MachineException)
            {
                return Failure(MachineErrors.CannotEject());
            }

            return Failure(originalError);
        }
    }
}
