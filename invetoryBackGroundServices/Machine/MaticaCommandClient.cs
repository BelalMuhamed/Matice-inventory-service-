using System;
using System.Threading;
using System.Threading.Tasks;
using MATICA_S3300e.LAN;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace invetoryBackGroundServices.Machine
{
    /// <summary>
    /// Matica-specific command construction and response interpretation, sitting on top of the
    /// vendor-neutral <see cref="IMachineTransport"/>. This is the layer that would be duplicated
    /// (not modified) to support a different device family later.
    /// <para>
    /// Scope note: this phase covers only the three commands currently exposed as standalone
    /// endpoints - <see cref="GetInfoAsync"/>, <see cref="RestoreAsync"/>,
    /// <see cref="EjectCardAsync"/>. The print-flow commands (LoadCard/ReadMAG/Emboss) migrate in
    /// the next phase, together with the tipper-parameter work, so the print path is changed once
    /// rather than twice. The pre-existing synchronous <c>MachineCommands</c> remains the code
    /// path for those commands until then.
    /// </para>
    /// </summary>
    public interface IMaticaCommandClient
    {
        /// <summary>
        /// Reads machine status, returning it parsed rather than as raw JSON text. Returns the
        /// full <see cref="MachineResponse"/> envelope - both <c>Machine_Configuration</c> and
        /// <c>Machine_Status</c> - since callers legitimately want either.
        /// </summary>
        Task<MachineResponse> GetInfoAsync(string ip, string port, CancellationToken cancellationToken);

        /// <summary>Issues the Restore (reset) command.</summary>
        Task RestoreAsync(string ip, string port, CancellationToken cancellationToken);

        /// <summary>Ejects the currently loaded card to the given stacker/hopper.</summary>
        Task EjectCardAsync(string ip, string port, int hopperId, CancellationToken cancellationToken);
    }

    /// <inheritdoc cref="IMaticaCommandClient" />
    public sealed class MaticaCommandClient : IMaticaCommandClient
    {
        private const string ActionPath = "action";

        private static readonly JsonSerializerSettings SerializerSettings = new()
        {
            ContractResolver = new DefaultContractResolver(),
            Formatting = Formatting.None
        };

        private readonly IMachineTransport _transport;

        /// <summary>Creates the command client over the given transport.</summary>
        public MaticaCommandClient(IMachineTransport transport) => _transport = transport;

        /// <inheritdoc />
        public async Task<MachineResponse> GetInfoAsync(string ip, string port, CancellationToken cancellationToken)
        {
            // Approved change: this uses the caller-supplied port like every other command,
            // rather than the port 33201 the pre-existing httpPOSTGetInfoJson hardcoded.
            // NOTE: the earlier POC validated this request shape against the real machine on port
            // 33201 specifically - it did not validate a different port. See the patch notes for
            // the two-call verification to run before relying on this.
            string body = await _transport.PostAsync(
                ip, port, ActionPath, "{\"Command\":\"GetInfoJson\"}", cancellationToken);

            MachineResponse? response;
            try
            {
                // MachineResponse, not MachineInfoJSON: the real reply is an envelope
                // ({"Answer":..,"Machine_Configuration":{..},"Machine_Status":{..}}) and
                // MachineInfoJSON is only the inner Machine_Status object. Deserializing the
                // envelope straight into MachineInfoJSON would not throw - it would silently
                // yield an object with every property null.
                response = JsonConvert.DeserializeObject<MachineResponse>(body);
            }
            catch (JsonException ex)
            {
                throw new MachineProtocolException("Machine status response could not be parsed.", ex);
            }

            if (response is null)
            {
                throw new MachineProtocolException("Machine status response was empty.");
            }

            if (response.MachineStatus is null)
            {
                // Parsed, but without the section that carries the actual status - treat as a
                // protocol error rather than handing the caller a hollow object.
                throw new MachineProtocolException("Machine status response contained no status section.");
            }

            return response;
        }

        /// <inheritdoc />
        public async Task RestoreAsync(string ip, string port, CancellationToken cancellationToken)
        {
            string body = await _transport.PostAsync(
                ip, port, ActionPath, "{\"Command\":\"Restore\"}", cancellationToken);

            EnsureAccepted(body);
        }

        /// <inheritdoc />
        public async Task EjectCardAsync(
            string ip, string port, int hopperId, CancellationToken cancellationToken)
        {
            var command = new EjectCardClass("EjectCard", hopperId.ToString());
            string json = JsonConvert.SerializeObject(command, SerializerSettings);

            string body = await _transport.PostAsync(ip, port, ActionPath, json, cancellationToken);

            EnsureAccepted(body);
        }

        /// <summary>
        /// Interprets the machine's <c>AnswerClass</c> envelope, turning a reported machine-side
        /// error into a typed <see cref="MachineRejectedException"/> instead of the old code's
        /// pattern of returning -1 with the raw text as the message.
        /// <para>
        /// The failure signal is <c>Answer == "KO"</c>, exactly as the pre-existing
        /// <c>httpPOST</c> determines it - not merely the presence of error text.
        /// <c>AnswerClass</c>'s constructor always allocates a non-null <c>Error</c>, so testing
        /// for a non-null error would report success as failure. The composed message preserves
        /// the same group/code/message detail the old path put into <c>sMessage</c>, but it
        /// travels as the localization argument rather than as the user-facing message itself.
        /// </para>
        /// </summary>
        private static void EnsureAccepted(string body)
        {
            AnswerClass? answer;
            try
            {
                answer = JsonConvert.DeserializeObject<AnswerClass>(body);
            }
            catch (JsonException ex)
            {
                throw new MachineProtocolException("Machine response could not be parsed.", ex);
            }

            if (answer is null)
            {
                throw new MachineProtocolException("Machine response was empty.");
            }

            if (string.Equals(answer.Answer, "KO", StringComparison.OrdinalIgnoreCase))
            {
                string detail = answer.Error is null
                    ? "no detail supplied"
                    : $"Group: {answer.Error.group}, ErrNumber: {answer.Error.code} - {answer.Error.message}";

                throw new MachineRejectedException(detail);
            }
        }
    }
}
