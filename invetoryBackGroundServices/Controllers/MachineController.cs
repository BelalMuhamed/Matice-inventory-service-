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
        /// Matica Print Flow. Rewritten from the ground up: the four legacy business lookups
        /// (MachineConfigrations/machines/details, PrintConfigurations/get-print-config-for-
        /// sepecific-face, Products/get-product-by-name, Branch/branches) are gone entirely -
        /// Angular now supplies everything they used to fetch directly in <paramref name="dto"/>,
        /// since it already has this data from its own normal Inventory API browsing. The two new
        /// calls this method makes instead - <see cref="IPrintFlowClient.ResolveForPrintAsync"/>
        /// (Backend Call #1, right after ReadMAG) and <see cref="IPrintFlowClient.RecordPrintResultAsync"/>
        /// (Backend Call #2, right after EjectCard) - own no business logic themselves; they call
        /// the Inventory API and react to what it says. This service never sees the Known-way/
        /// Unknown-way distinction at all.
        /// </summary>
        [HttpPost("Print-Card-Holder-Name")]
        public async Task<IActionResult> Print([FromBody] PrintReqDto dto, CancellationToken cancellationToken)
        {
            if (IsOutsideTokenScope(dto.BranchId))
            {
                return Forbid();
            }

            // Generated once per physical print attempt, reused on every retry of Backend Call #2
            // (see PrintFlowClient.RecordPrintResultAsync) - never regenerated mid-attempt.
            string idempotencyKey = Guid.NewGuid().ToString();

            string localBatch;
            string logData;

            ConnectionInfo.ip = UTILITIES.FormatIP(out ERROR, dto.MachineIp);
            ConnectionInfo.port = dto.Port;
            httpAction.sAction = "action";
            Data.FeederID = dto.FeederId.ToString();
            Data.StackerID = dto.HopperId.ToString();
            Data.ReadTrackID = "2";

            #region get machine info and check status
            int Reply = MachineComm.CommandManagement(MachineCommands.Commands.GetInfoJson, ref InfoText);
            if (MachineComm.ValuateReply(Reply))
            {
                return BadRequest(new { success = false, message = enumType.Error + InfoText });
            }

            // Matica Print Flow, status-parsing fix: structured deserialization into MachineInfoJSON
            // instead of raw string.Contains(...) against the response text. Only MachineStatus/
            // CardInside/TipperStatus are confirmed wire field names (see CardDataBean.cs) - this
            // check only relies on those three, so it doesn't depend on any of the unconfirmed ones.
            MachineResponse? status;
            try
            {
                status = Newtonsoft.Json.JsonConvert.DeserializeObject<MachineResponse>(InfoText);
            }
            catch (Newtonsoft.Json.JsonException)
            {
                status = null;
            }

            if (status is null
                || status.MachineStatus.machineStatus != "READY"
                || status.MachineStatus.CardInside != "no"
                || status.MachineStatus.TipperStatus != "Ready")
            {
                return BadRequest(new { success = false, message = "machine isn't ready to print yet !" });
            }
            #endregion

            #region loadcard
            Reply = MachineComm.CommandManagement(MachineCommands.Commands.LoadCard, ref InfoText);
            if (MachineComm.ValuateReply(Reply))
            {
                return BadRequest(new { success = false, message = enumType.Error + InfoText });
            }
            #endregion

            #region ReadMAGData
            Reply = MachineComm.CommandManagement(MachineCommands.Commands.ReadMAG, ref InfoText);
            if (MachineComm.ValuateReply(Reply))
            {
                for (int i = 0; i < 20; i++) Data.EmbossLineText[i] = string.Empty;

                Reply = MachineComm.CommandManagement(MachineCommands.Commands.EjectCard, ref InfoText);
                if (MachineComm.ValuateReply(Reply))
                {
                    return BadRequest(new { success = false, message = "cann't eject card   !" });
                }
                return BadRequest(new { success = false, message = enumType.Error + InfoText });
            }

            // Raw PAN, used only transiently for Backend Call #1 below - never logged, never
            // assigned anywhere else, never persisted locally. Matica Print Flow, raw PAN handling.
            string rawPan = MachineComm.sReadMAGData.Substring(0, 16);
            #endregion

            #region Backend Call #1: resolve-for-print
            PrintFlowResult<ResolveForPrintResponseDto> resolveResult = await _printFlowClient.ResolveForPrintAsync(
                GetBearerToken(), rawPan, dto.ProductId, dto.BranchId, cancellationToken);
            // rawPan is not referenced again after this call.

            if (!resolveResult.Success)
            {
                for (int i = 0; i < 20; i++) Data.EmbossLineText[i] = string.Empty;
                _log.AppendLog(
                    " [Card Holder Name:" + dto.CardHolderName + "] [Product:" + dto.ProductId +
                    "] resolve-for-print failed: " + resolveResult.ErrorMessage, Logger.LogType.Error);

                Reply = MachineComm.CommandManagement(MachineCommands.Commands.EjectCard, ref InfoText);
                if (MachineComm.ValuateReply(Reply))
                {
                    return BadRequest(new { success = false, message = "cann't eject card   !" });
                }

                string reason = resolveResult.IsTransient
                    ? "cann't validate this card right now, please check server connection !"
                    : (resolveResult.ErrorMessage ?? "card not found !");
                return BadRequest(new { success = false, message = reason });
            }

            long productItemId = resolveResult.Data!.ProductItemId;
            string maskedPan = resolveResult.Data.MaskedPan;
            #endregion

            #region Load Embossing information
            for (int i = 0; i < 20; i++) Data.EmbossLineText[i] = string.Empty;
            Data.EmbossLineText[0] = dto.CardHolderName.Trim();
            Data.EmbossLineFont[0] = dto.Font.ToString();
            Data.EmbossLineCpi[0] = dto.Cpi.ToString();
            Data.EmbossLineX[0] = dto.OffsetX.ToString();
            Data.EmbossLineY[0] = dto.OffsetY.ToString();
            Data.TipperEnable = "Y";
            #endregion

            #region EmbossCardHolderName
            Reply = MachineComm.CommandManagement(MachineCommands.Commands.Emboss, ref InfoText);
            bool printSucceeded = !MachineComm.ValuateReply(Reply);
            #endregion

            #region save to local batch
            logData = " [Card Holder Name:" + dto.CardHolderName + "] [Product:" + dto.ProductId +
                "] [Card PAN:" + maskedPan + "] [Status:" + (printSucceeded ? "Success" : "Error") + "]";
            _log.AppendLog(logData, printSucceeded ? Logger.LogType.Info : Logger.LogType.Error);

            localBatch = Path.Combine(AppContext.BaseDirectory, "LocalBatch.lbt");
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

            for (int i = 0; i < 20; i++) Data.EmbossLineText[i] = string.Empty;

            #region EjectCard
            // Eject before Backend Call #2, deliberately: the physical card is already
            // printed-or-spoiled by this point, so it goes to the operator/customer promptly
            // instead of sitting inside the machine for the duration of Backend Call #2's retry
            // loop (up to a few seconds on a transient failure).
            Reply = MachineComm.CommandManagement(MachineCommands.Commands.EjectCard, ref InfoText);
            bool ejectSucceeded = !MachineComm.ValuateReply(Reply);
            #endregion

            #region Backend Call #2: print-result
            PrintFlowResult<object?> recordResult = await _printFlowClient.RecordPrintResultAsync(
                GetBearerToken(), productItemId, dto.BranchId, printSucceeded, dto.CardHolderName.Trim(),
                idempotencyKey, cancellationToken);
            #endregion

            if (!ejectSucceeded)
            {
                return BadRequest(new { success = false, message = "card printed but failed to eject card !" });
            }

            if (!printSucceeded)
            {
                return BadRequest(new { success = false, message = "Error Printing Card" });
            }

            if (!recordResult.Success)
            {
                // The physical print already succeeded and the card has been ejected - this is the
                // "printer succeeds, backend logging fails" scenario from the plan. Told to the
                // caller distinctly rather than as an ordinary failure, since the card itself is
                // fine; only the Inventory API's record of it could not be confirmed.
                _log.AppendLog(
                    $"print-result could not be confirmed for item {productItemId}, idempotencyKey {idempotencyKey}: " +
                    recordResult.ErrorMessage, Logger.LogType.Error);

                return StatusCode(207, new
                {
                    success = true,
                    message = "card printed and ejected, but the Inventory API could not confirm the result - " +
                               "please verify manually.",
                    productItemId,
                    idempotencyKey
                });
            }

            return Ok(new { success = true, message = "card printed " });
        }
    }
}
