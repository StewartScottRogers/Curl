using System.Globalization;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Smb;

/// <summary>
/// Serves the <c>smb</c> and <c>smbs</c> schemes on every platform, speaking curl 8.21.0's
/// SMBv1 (ADR-0200) to download a file, or with <c>-T</c> to upload one: parses the share
/// and file from the URL's path, connects, negotiates the <c>NT LM 0.12</c> dialect, sets
/// up a session authenticated with NTLMv1 responses, then connects to the share, reads the
/// file to the output or writes the upload source to it, created or truncated, and
/// disconnects (<see cref="SmbFileTransfer" />).
/// </summary>
/// <remarks>
/// The connector supplies the connection to the URL's host and port, in TLS from the
/// first byte for <c>smbs</c>; no <see cref="System.Net.Sockets.Socket" /> is ever
/// constructed here. The outcomes, in curl's order: a path with no share is refused before connecting with
/// exit 3; a connect failure is returned as the connector reported it; a transfer with no
/// user (<see cref="ITransferContext.Credentials" />) is refused once connected with exit
/// 67 and nothing sent; then <see cref="SmbSessionEstablisher" />'s outcomes; then an
/// upload source that cannot seek, such as <c>-T -</c>'s standard input, is refused with
/// exit 55 and nothing more sent, as curl refuses an upload of unknown size; then
/// <see cref="SmbFileTransfer" />'s. A connection write or read that throws an
/// <see cref="IOException" /> at any step ends the transfer with exit 55 or 56
/// (<see cref="SmbIoFailures" />). The port
/// defaults to 445 for both schemes. A server that closes the connection mid-exchange is
/// waited on until <see cref="ITransferContext.CancellationToken" /> ends the transfer, as
/// curl waits for <c>-m</c>. Each step is written to
/// <see cref="ITransferContext.DiagnosticLog" /> under the <c>smb</c> component
/// (<see cref="SmbTransferLog" />).
/// </remarks>
public sealed class SmbProtocolHandler : IProtocolHandler
{
    /// <summary>The user name or password held a byte below 0x20 (exit 43), curl 8.22.0's text (BL-2179).</summary>
    private const string CredentialsHaveControlCode = "control code detected in credentials";

    private const int DefaultPort = 445;

    private const string SecureScheme = "smbs";

    // The number curl names a connection it never opened by, as in "closing connection #-1".
    private const long NoConnectionNumber = -1;

    private static readonly string[] Schemes = ["smb", SecureScheme];

    private readonly IConnector connector;

    private readonly string operatingSystem;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmbProtocolHandler" /> class that sends
    /// the host triple of the platform this process runs on.
    /// </summary>
    /// <param name="connector">Supplies the connection to the URL's host and port.</param>
    public SmbProtocolHandler(IConnector connector)
        : this(connector, SmbCurlOperatingSystem.Current)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SmbProtocolHandler" /> class that sends
    /// <paramref name="operatingSystem" /> as curl's host triple.
    /// </summary>
    /// <param name="connector">Supplies the connection to the URL's host and port.</param>
    /// <param name="operatingSystem">The host triple to send in the session setup.</param>
    /// <exception cref="ArgumentNullException"><paramref name="connector" /> is <see langword="null" />.</exception>
    internal SmbProtocolHandler(IConnector connector, string operatingSystem)
    {
        this.connector = connector ?? throw new ArgumentNullException(nameof(connector));
        this.operatingSystem = operatingSystem;
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
        TransferResult result = await ConnectAndTransferAsync(context).ConfigureAwait(false);
        new SmbTransferLog(context.DiagnosticLog).Ended(result, context.TimeProvider.GetElapsedTime(started));
        return result;
    }

    /// <summary>
    /// The exit 43 curl 8.22.0 refuses <paramref name="credentials" /> with before it connects when the
    /// user name or password holds a byte below 0x20, or <see langword="null" /> when neither does (BL-2179).
    /// </summary>
    private static TransferResult? RefuseControlBytes(System.Net.NetworkCredential? credentials) =>
        credentials is not null && (credentials.UserName.Any(character => character < ' ') || credentials.Password.Any(character => character < ' '))
            ? TransferResult.Failure(CurlExitCode.BadFunctionArgument, CredentialsHaveControlCode)
            : null;

    private async ValueTask<TransferResult> ConnectAndTransferAsync(ITransferContext context)
    {
        if (RefuseControlBytes(context.Credentials) is { } credentialsRefused)
        {
            return credentialsRefused;
        }

        long started = context.TimeProvider.GetTimestamp();
        CurlUrl url = context.Url;
        if (SmbUrlPath.TryParse(url.AbsolutePath, out SmbUrlPath? path) is { } pathError)
        {
            TransferResult refused = TransferResult.Failure(CurlExitCode.UrlMalformat, pathError);
            ReportConnectionEnd(context.Events, refused, closes: true, NoConnectionNumber);
            return refused;
        }

        var target = new ConnectTarget(url.IdnHost, url.IsDefaultPort ? DefaultPort : url.Port, url.Scheme == SecureScheme)
        {
            Proxy = context.Proxy,
            Events = context.Events,
            DiagnosticLog = context.DiagnosticLog,
        };
        ConnectResult connect = await connector.ConnectAsync(target, context.CancellationToken).ConfigureAwait(false);
        if (connect.Connection is not { } connection)
        {
            return new TransferResult(connect.ExitCode, 0, connect.ErrorMessage) { IsConnectionRefused = connect.IsConnectionRefused };
        }

        TransferResult result;
        bool closes;
        context.Progress.ReportTransferStarted();
        var receiveCount = new SmbReceiveCount();
        var counting = new SmbCountingConnection(connection, context.TimeProvider);
        await using (counting.ConfigureAwait(false))
        {
            try
            {
                (result, closes) = await TransferAsync(counting, context, path!, receiveCount).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (context.MaxTime is not null && context.CancellationToken.IsCancellationRequested)
            {
                ReportTimedOut(context, receiveCount, started, connect.ConnectionNumber);
                throw;
            }
        }

        ReportConnectionEnd(context.Events, result, closes, connect.ConnectionNumber);
        return StampReport(result, context.TimeProvider, context.Upload is not null, new TransferTimings(started, connect.Timings, null, null, null, 0), counting);
    }

    // curl's failf writes the -m timeout as a -v line before it closes the connection; the
    // watchdog gives the transfer its exit 28 message from the same counts (BL-5329).
    private static void ReportTimedOut(ITransferContext context, SmbReceiveCount receiveCount, long started, long connectionNumber)
    {
        long operationStarted = context.OperationStarted ?? started;
        context.Events.ReportInfo(receiveCount.TimedOutMessage((long)context.TimeProvider.GetElapsedTime(operationStarted).TotalMilliseconds));
        context.Events.ReportInfo(string.Create(CultureInfo.InvariantCulture, $"closing connection #{connectionNumber}"));
    }

    // Adds the timings behind %{time_*} and %{speed_*} and the request bytes behind %{size_request}
    // to the report the transfer made, keeping what it already reported such as the upload size (BL-5328).
    private static TransferResult StampReport(TransferResult result, TimeProvider timeProvider, bool isUpload, TransferTimings reached, SmbCountingConnection counting)
    {
        long completed = timeProvider.GetTimestamp();
        // curl's SMB handler does all its work in the protocol-do step, so %{time_pretransfer} lands
        // with the end of the transfer, and an upload never starts a transfer (%{time_starttransfer} 0) (BL-5476).
        TransferTimings timings = reached with { RequestReady = completed, FirstByteReceived = isUpload ? null : counting.FirstReadAt, Completed = completed };
        TransferReport report = (result.Report ?? new TransferReport()) with { Timings = timings, RequestSize = counting.BytesWritten, DownloadSize = isUpload ? 0 : result.BytesTransferred };
        return result with { Report = report };
    }

    /// <summary>
    /// Reports the <c>-v</c> lines curl 8.21.0 ends an SMB transfer with (measured, BL-598
    /// Notes): a failure's text when curl reports it through <c>failf</c>, then
    /// <c>closing connection #N</c> for a failure before the session is set up (curl's
    /// connect phase, which ends the connection as aborted) and <c>shutting down connection #N</c>
    /// for any other outcome.
    /// </summary>
    private static void ReportConnectionEnd(ITransferEvents events, TransferResult result, bool closes, long connectionNumber)
    {
        if (result.ErrorMessage is { } message && SmbMessages.IsVerboseLine(message))
        {
            events.ReportInfo(message);
        }

        events.ReportInfo(string.Create(CultureInfo.InvariantCulture, $"{(closes ? "closing" : "shutting down")} connection #{connectionNumber}"));
    }

    // Returns the outcome and whether it ended curl's connect phase (no user, or a session
    // that was not set up), which curl ends with "closing connection" rather than
    // "shutting down connection".
    private async ValueTask<(TransferResult Result, bool Closes)> TransferAsync(IConnection connection, ITransferContext context, SmbUrlPath path, SmbReceiveCount receiveCount)
    {
        if (context.Credentials is not { } credentials)
        {
            return (TransferResult.Failure(CurlExitCode.LoginDenied, SmbMessages.LoginDenied), true);
        }

        var transferLog = new SmbTransferLog(context.DiagnosticLog);
        var reader = new SmbMessageReader(connection, context.TimeProvider, transferLog);
        var establisher = new SmbSessionEstablisher(connection, reader, operatingSystem, transferLog);
        (ushort userId, TransferResult? failure) = await establisher.EstablishAsync(
            credentials.Password,
            SmbIdentity.Split(credentials.UserName, context.Url.IdnHost),
            context.CancellationToken).ConfigureAwait(false);
        if (failure is not null)
        {
            return (failure, true);
        }

        TransferResult result = context.Upload is { CanSeek: false }
            ? TransferResult.Failure(CurlExitCode.SendError, SmbMessages.UploadSizeUnknown)
            : await new SmbFileTransfer(connection, reader, userId, context) { ReceiveCount = receiveCount }
                .TransferAsync(context.Url.IdnHost, path)
                .ConfigureAwait(false);
        return (result, false);
    }
}
