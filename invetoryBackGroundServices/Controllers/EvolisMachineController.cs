using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using invetoryBackGroundServices.Common;
using invetoryBackGroundServices.Machine.Evolis;
using invetoryBackGroundServices.Params;
using invetoryBackGroundServices.Security;
using invetoryBackGroundServices.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace invetoryBackGroundServices.Controllers
{
    /// <summary>
    /// Evolis device-proxy endpoint (Evolis Integration Plan §8, decision Q-22) — exposes what
    /// <c>Evolis.GetDevices()</c> actually finds on this machine, for Angular to join against the
    /// Inventory API's own <c>GET /api/printers</c> (plan §4.4). Requires a valid Print Agent
    /// token, same as <see cref="MachineController"/>.
    /// </summary>
    [Route("api/machine/evolis")]
    [ApiController]
    [Authorize(Policy = PrintAgentAuthPolicy.Name)]
    public sealed class EvolisMachineController : ControllerBase
    {
        private readonly EvolisDeviceDiscovery _discovery;
        private readonly EvolisSupervisorRegistrar _registrar;
        private readonly EvolisConnectionFactory _connectionFactory;

        /// <summary>Creates the controller.</summary>
        public EvolisMachineController(
            EvolisDeviceDiscovery discovery, EvolisSupervisorRegistrar registrar, EvolisConnectionFactory connectionFactory)
        {
            _discovery = discovery;
            _registrar = registrar;
            _connectionFactory = connectionFactory;
        }

        /// <summary>
        /// Lists every Evolis device this machine can see right now, each briefly probed for
        /// status and loaded ribbon. Returns an empty list (not an error) when no device is
        /// present or no driver is installed — plan §8 acceptance criteria.
        /// </summary>
        /// <response code="200">Every discovered Evolis device, possibly empty.</response>
        [HttpGet("printers")]
        [ProducesResponseType(typeof(ApiResponse<EvolisDiscoveryResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetPrinters(CancellationToken cancellationToken)
        {
            // EvolisDeviceDiscovery.DiscoverAll() is synchronous, blocking SDK I/O per device
            // (open, GetStatus, GetRibbonInfo, close) - offloaded to a thread-pool thread so a
            // multi-device or slow-to-respond probe doesn't tie up this request's own thread for
            // the full hardware round-trip, which matters for the polling access pattern Angular
            // uses against this endpoint.
            List<EvolisDiscoveredPrinterDto> devices =
                await Task.Run(() => _discovery.DiscoverAll(), cancellationToken);
            var response = new EvolisDiscoveryResponse(Environment.MachineName, devices);

            // Plan §8: self-registration also runs "after any manual discovery call", not just at
            // startup. CancellationToken.None deliberately, not the request's own token — this
            // keeps running after the HTTP response has already been written.
            _ = _registrar.RegisterNowAsync(CancellationToken.None);

            return Ok(ApiResponse<EvolisDiscoveryResponse>.Ok(response));
        }

        /// <summary>
        /// Resets a printer stuck in a mechanical-error state back to ready (Evolis Integration
        /// Plan §11's hardening item: "<c>ClearMechanicalErrors()</c> exposed as an admin endpoint,
        /// not called automatically") — <c>Connection.ClearMechanicalErrors()</c>, confirmed real
        /// and parameterless on the installed SDK. Deliberately not invoked anywhere in the print
        /// flow itself: a mechanical fault means the printer needs a human to look at it, not a
        /// silent auto-recovery that could mask a recurring hardware problem.
        /// </summary>
        /// <param name="deviceName">The SDK connection name (<c>Evolis.Device.name</c>) of the printer to reset.</param>
        /// <response code="200">The printer accepted the reset and reports ready.</response>
        /// <response code="409">
        /// <c>Connection.ClearMechanicalErrors()</c> itself returned false — the printer refused
        /// the reset, most likely because the underlying mechanical condition is still present.
        /// </response>
        [HttpPost("printers/{deviceName}/clear-mechanical-errors")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> ClearMechanicalErrors(string deviceName, CancellationToken cancellationToken)
        {
            // Opened without reserving: a printer stuck on a mechanical fault may well refuse
            // Reserve() itself (the SDK's own doc comment for this method says the printer "will
            // not accept any other job" while the fault is active), so the reset must not depend
            // on a reservation succeeding first.
            bool cleared = await Task.Run(() =>
            {
                using EvolisConnectionScope scope = _connectionFactory.Open(deviceName, reserve: false);
                return scope.Connection.ClearMechanicalErrors();
            }, cancellationToken);

            if (!cleared)
            {
                throw new EvolisException(EvolisErrors.MechanicalFailure("ClearMechanicalErrors() returned false"), "Could not clear the printer's mechanical error.");
            }

            return Ok(ApiResponse<object?>.Ok(null));
        }
    }
}
