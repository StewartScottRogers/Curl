using System.Security.Cryptography;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Mqtt;

/// <summary>
/// Serves the <c>mqtt</c> and <c>mqtts</c> schemes as curl 8.21.0 does: publishes the
/// <c>-d</c> data to the URL's topic, or without it subscribes to the topic and writes
/// every message the broker publishes to it.
/// </summary>
/// <remarks>
/// <para>
/// The connection comes from the injected <see cref="IConnector" /> (ADR-0005): port 1883
/// for <c>mqtt</c> and 8883 for <c>mqtts</c> unless the URL names one, and
/// <c>mqtts</c> differs only in asking for TLS. No <see cref="System.Net.Sockets.Socket" />
/// or <c>SslStream</c> is constructed here.
/// </para>
/// <para>
/// Every CONNECT carries <see cref="ITransferContext.Credentials" /> when they are set: the
/// user name and the password in UTF-8, each only when it is not empty. Choosing between
/// <c>-u</c> and the URL's user information is the command line's job (ADR-0006).
/// </para>
/// <para>
/// With <see cref="ITransferContext.PostData" /> set, the handler sends one PUBLISH at QoS 0
/// without the retain flag, the only kind curl sends, then DISCONNECT, and succeeds without
/// waiting for the broker or writing anything to the output. A PUBLISH over curl's
/// 268435455-byte limit is exit 100 (<see cref="CurlExitCode.TooLarge" />).
/// </para>
/// <para>
/// Without it, each PUBLISH received is written to <see cref="ITransferContext.Output" /> as
/// curl writes it: two bytes of topic length, the topic, then the payload. The transfer runs
/// until the broker ends it: a DISCONNECT is a success, and a closed connection is exit 56
/// (<see cref="CurlExitCode.RecvError" />, <c>Connection disconnected</c>), which is how a
/// subscribe usually ends. A PUBLISH whose remaining length is over
/// <see cref="ITransferContext.MaxFileSize" /> is exit 63
/// (<see cref="CurlExitCode.FilesizeExceeded" />) with none of it written; the limit applies
/// to each PUBLISH on its own. Under <see cref="ITransferContext.NoBody" /> (<c>-I</c>) the
/// first PUBLISH body is exit 8 (<see cref="CurlExitCode.WeirdServerReply" />) with nothing
/// written, as curl 8.21.0 does (BL-1310).
/// </para>
/// <para>
/// Either way, a refused CONNACK, any packet curl does not expect and a user name or
/// password over 65535 bytes are exit 8 (<see cref="CurlExitCode.WeirdServerReply" />); a
/// URL with no topic is exit 3 (<see cref="CurlExitCode.UrlMalformat" />), found after the
/// CONNACK as in curl. A failure of the connection, or an <see cref="IOException" /> from
/// the output, is returned rather than thrown; cancellation leaves as an exception.
/// </para>
/// <para>
/// Each step is written to <see cref="ITransferContext.DiagnosticLog" />, component
/// <c>mqtt</c>, which the <see cref="ConnectTarget" /> carries on to the connector
/// (ADR-0222, BL-928); see <see cref="MqttDiagnosticLog" />.
/// </para>
/// </remarks>
public sealed class MqttProtocolHandler : IProtocolHandler
{
    private const int MqttDefaultPort = 1883;

    private const int MqttsDefaultPort = 8883;

    private const int ClientIdentifierSuffixLength = 8;

    private const string ClientIdentifierPrefix = "curl";

    private const string ClientIdentifierAlphabet =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    /// <summary>
    /// The two schemes this handler serves, as curl 8.21.0's <c>--version</c> protocol
    /// list names them.
    /// </summary>
    private static readonly string[] Schemes = ["mqtt", "mqtts"];

    private readonly IConnector connector;

    private readonly Func<string> clientIdentifierSuffixSource;

    /// <summary>
    /// Initializes a new instance of the <see cref="MqttProtocolHandler" /> class whose
    /// client identifiers are <c>curl</c> and eight random letters and digits, as curl's.
    /// </summary>
    /// <param name="connector">The connector each transfer's connection comes from.</param>
    /// <exception cref="ArgumentNullException"><paramref name="connector" /> is <see langword="null" />.</exception>
    public MqttProtocolHandler(IConnector connector)
        : this(connector, CreateRandomClientIdentifierSuffix)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MqttProtocolHandler" /> class whose
    /// client identifiers are <c>curl</c> followed by what
    /// <paramref name="clientIdentifierSuffixSource" /> returns.
    /// </summary>
    /// <param name="connector">The connector each transfer's connection comes from.</param>
    /// <param name="clientIdentifierSuffixSource">
    /// Produces the characters after <c>curl</c> in each CONNECT's client identifier: eight
    /// ASCII letters and digits, so a test can fix what curl leaves to chance.
    /// </param>
    /// <exception cref="ArgumentNullException">Either argument is <see langword="null" />.</exception>
    public MqttProtocolHandler(IConnector connector, Func<string> clientIdentifierSuffixSource)
    {
        this.connector = connector ?? throw new ArgumentNullException(nameof(connector));
        this.clientIdentifierSuffixSource = clientIdentifierSuffixSource
            ?? throw new ArgumentNullException(nameof(clientIdentifierSuffixSource));
    }

    /// <inheritdoc />
    public IReadOnlyCollection<string> SupportedSchemes => Schemes;

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">
    /// <paramref name="context" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <see cref="ITransferContext.CancellationToken" /> was cancelled.
    /// </exception>
    public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        long started = context.TimeProvider.GetTimestamp();
        MqttDiagnosticLog log = new(context.DiagnosticLog);
        TransferResult result = await TransferAsync(context, log).ConfigureAwait(false);
        log.TransferEnded(result, context.TimeProvider.GetElapsedTime(started));
        return result;
    }

    private static ConnectTarget CreateTarget(ITransferContext context)
    {
        CurlUrl url = context.Url;
        bool useTls = url.Scheme == "mqtts";
        int defaultPort = useTls ? MqttsDefaultPort : MqttDefaultPort;

        return new ConnectTarget(url.IdnHost, url.IsDefaultPort ? defaultPort : url.Port, useTls)
        {
            Proxy = context.Proxy,
            Events = context.Events,
            DiagnosticLog = context.DiagnosticLog,
        };
    }

    private static string CreateRandomClientIdentifierSuffix() =>
        RandomNumberGenerator.GetString(ClientIdentifierAlphabet, ClientIdentifierSuffixLength);

    /// <summary>
    /// Connects, then runs the session over the connection and disposes it.
    /// </summary>
    private async ValueTask<TransferResult> TransferAsync(ITransferContext context, MqttDiagnosticLog log)
    {
        ConnectResult connected = await connector
            .ConnectAsync(CreateTarget(context), context.CancellationToken)
            .ConfigureAwait(false);
        if (connected.Connection is not { } connection)
        {
            return new TransferResult(connected.ExitCode, 0, connected.ErrorMessage) { IsConnectionRefused = connected.IsConnectionRefused };
        }

        context.Progress.ReportTransferStarted();
        TransferResult result;
        string? followingLine = null;
        await using (connection.ConfigureAwait(false))
        {
            MqttSession session = new(connection, context.Output, context.Progress, context.Events, log, context.MaxFileSize, context.NoBody, context.TimeProvider, context.CancellationToken);
            try
            {
                await session
                    .RunAsync(
                        context.Url,
                        ClientIdentifierPrefix + clientIdentifierSuffixSource(),
                        context.Credentials,
                        context.PostData)
                    .ConfigureAwait(false);
                result = TransferResult.Success(session.BytesWritten);
            }
            catch (MqttTransferException failure)
            {
                result = new TransferResult(failure.ExitCode, session.BytesWritten, failure.Message);
                followingLine = failure.FollowingLine;
            }
        }

        ReportConnectionEnd(context.Events, result, followingLine, connected.ConnectionNumber);
        return result;
    }

    /// <summary>
    /// Reports how the transfer ended, as curl 8.21.0's <c>-v</c> does (measured, BL-935):
    /// the failure's message unless curl prints it without <c>failf</c>, then the line
    /// <c>lib/mqtt.c</c> adds after it (BL-1229), then <c>closing connection #N</c> after an
    /// output write failure and <c>shutting down connection #N</c> after anything else.
    /// </summary>
    private static void ReportConnectionEnd(ITransferEvents events, TransferResult result, string? followingLine, long connectionNumber)
    {
        if (result.ErrorMessage is { } message && !MqttTransferMessages.IsStrerrorText(message))
        {
            events.ReportInfo(message);
        }

        if (followingLine is not null)
        {
            events.ReportInfo(followingLine);
        }

        events.ReportInfo(result.ExitCode == CurlExitCode.WriteError
            ? MqttTransferMessages.ClosingConnection(connectionNumber)
            : MqttTransferMessages.ShuttingDownConnection(connectionNumber));
    }
}
