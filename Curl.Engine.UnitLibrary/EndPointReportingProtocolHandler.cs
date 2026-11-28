using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Performs each transfer with <paramref name="handler" /> and puts on its report the end
/// points of the first connection it opened through the recording connectors, so every
/// scheme's handler reports them without copying them itself (ADR-0119).
/// </summary>
/// <param name="handler">The handler that performs the transfer.</param>
/// <param name="recorder">The recorder the handler's connectors record into.</param>
internal sealed class EndPointReportingProtocolHandler(IProtocolHandler handler, ConnectionEndPointRecorder recorder) : IProtocolHandler
{
    /// <summary>Gets the schemes <see cref="Handler" /> serves.</summary>
    public IReadOnlyCollection<string> SupportedSchemes => handler.SupportedSchemes;

    /// <summary>
    /// Gets the handler that performs the transfer: a <see cref="LazyProtocolHandler" />'s
    /// built handler, building it if it has not been yet.
    /// </summary>
    internal IProtocolHandler Handler => handler is LazyProtocolHandler lazy ? lazy.Handler : handler;

    /// <summary>Gets the handler as given, a <see cref="LazyProtocolHandler" /> left unbuilt.</summary>
    internal IProtocolHandler GivenHandler => handler;

    /// <summary>The <see cref="Exception.Data" /> key under which a cancelled transfer's <see cref="TransferReport" /> travels.</summary>
    internal const string ReportDataKey = "Curl.Engine.TransferReport";

    /// <summary>Gets the report <paramref name="cancelled" /> carries, or <see langword="null" /> when the transfer never connected.</summary>
    /// <param name="cancelled">The cancellation that left a handler.</param>
    /// <returns>The report put on it.</returns>
    internal static TransferReport? ReportCarriedBy(OperationCanceledException cancelled) =>
        cancelled.Data[ReportDataKey] as TransferReport;

    /// <inheritdoc />
    public ValueTask TransferChainFinishedAsync(ITransferContext first, TransferResult result) =>
        handler.TransferChainFinishedAsync(first, result);

    /// <summary>
    /// Clears the recorder, performs the transfer with <see cref="Handler" />, and puts the
    /// recorded end points on its report (<see cref="ConnectionEndPointRecorder.ReportOn" />).
    /// </summary>
    /// <param name="context">The transfer.</param>
    /// <returns>The handler's result, with the end points on its report.</returns>
    public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
    {
        recorder.Clear(context.Http?.RedirectsFollowed > 0);
        recorder.TransferClock = context.TimeProvider;
        recorder.ReportsEndPointsOfFailedConnects = IsExtendedScheme(context);
        long started = context.TimeProvider.GetTimestamp();
        long uploadStart = UploadPosition(context);
        TransferResult result;
        try
        {
            result = await handler.ExecuteAsync(context).ConfigureAwait(false);
        }
        catch (DatagramPortUnreachableException refused)
        {
            result = TransferResult.Failure(CurlExitCode.CouldntConnect, refused.Message);
        }
        catch (OperationCanceledException cancelled)
        {
            // A watchdog's cancellation leaves no result: carry the transfer's report on the exception for the caller that ends it (BL-5491), a UDP channel's or a handshake still running's too, so -w knows its total time (BL-5843); a report the handler carried keeps its timings and header count (BL-5862).
            TransferReport? handlerReport = ReportCarriedBy(cancelled);
            TransferResult connected = recorder.ReportOn(
                TransferResult.Failure(CurlExitCode.OperationTimedOut, cancelled.Message) with { Report = handlerReport },
                handlerCountsConnections: handlerReport is not null && CountsItsConnections(context));
            cancelled.Data[ReportDataKey] = StampTimingsAndUploadSize(connected, context, started, UploadedBy(context, UploadPosition(context) - uploadStart, true), true).Report;
            throw;
        }

        result = recorder.ReportOn(result, handlerCountsConnections: result.Report is not null && CountsItsConnections(context));
        return StampTimingsAndUploadSize(result, context, started, UploadedBy(context, UploadPosition(context) - uploadStart, result.IsSuccess));
    }

    // A scheme curl itself does not serve is an extension's, which reports the end points of a TLS handshake -m ended (EXT-097, ADR-1475).
    private static bool IsExtendedScheme(ITransferContext context) =>
        !ManagedCurlVersion.Protocols.Contains(context.Url.Scheme, StringComparer.OrdinalIgnoreCase);

    // Only the HTTP handler counts its connections on its report; every other scheme's are counted by the recorder.
    private static bool CountsItsConnections(ITransferContext context) => context.Url.Scheme is "http" or "https";

    // Gives a report with no timings the transfer's connect and total times and, when it sent none itself, the bytes the upload stream gave (BL-5410). One that never connected sent nothing. A report with timings keeps them, but for the start of a timed-out one.
    private TransferResult StampTimingsAndUploadSize(TransferResult result, ITransferContext context, long started, long uploaded, bool cancelled = false)
    {
        if (!cancelled && StaysWithoutReport(result, context))
        {
            return result;
        }

        TransferReport report = result.Report ?? new TransferReport { DownloadSize = result.BytesTransferred };
        if (report.Timings is { } own)
        {
            return StartedFrom(result, own, TimedFrom(result, context, own.Started));
        }

        return result with { Report = TimedReport(report, result.IsSuccess, context, TimedFrom(result, context, started), UploadSizeOf(report, result.IsSuccess, uploaded)) };
    }

    // A failure with no report stays without one (BL-5421), unless it is a connect failure curl times.
    private static bool StaysWithoutReport(TransferResult result, ITransferContext context) =>
        result.Report is null && !result.IsSuccess && !IsUnreportedConnectFailure(result, context);

    // A connect that could not resolve its name or timed out leaves no report, yet curl reports its elapsed %{time_total} (BL-6041, measured for ftp, wss and dict). An http or https one stays without a report, which libcurl's CURLINFO_NUM_CONNECTS reads (BL-3302).
    private static bool IsUnreportedConnectFailure(TransferResult result, ITransferContext context) =>
        result.ExitCode is CurlExitCode.CouldntResolveProxy or CurlExitCode.CouldntResolveHost or CurlExitCode.OperationTimedOut && !CountsItsConnections(context);

    // -m counts from its watchdog's start, before the handler's, so a timed-out first hop's total counts from there and is not under -m's value, as curl's is not (STD-231). A later hop's counts from its own start, as the redirect follower measures the whole from the first hop's.
    private static long TimedFrom(TransferResult result, ITransferContext context, long started) =>
        result.ExitCode == CurlExitCode.OperationTimedOut && context.Http?.RedirectsFollowed is not > 0 ? context.OperationStarted ?? started : started;

    // The result with its handler's own timings, as HTTP's, counted from started: the result itself when they already are.
    private static TransferResult StartedFrom(TransferResult result, TransferTimings own, long started) =>
        started == own.Started ? result : result with { Report = result.Report! with { Timings = own with { Started = started } } };

    // The report with its timings and upload size, and a UDP channel's local end when the scheme reports one (BL-5493).
    private TransferReport TimedReport(TransferReport report, bool succeeded, ITransferContext context, long started, long uploadSize)
    {
        long completed = context.TimeProvider.GetTimestamp();
        if (!ReportsPhases(context))
        {
            return report with { Timings = new TransferTimings(started, recorder.FirstConnectTimings, null, null, null, completed), UploadSize = uploadSize };
        }

        TransferTimings timings = new(started, ConnectTimingsOf(started, completed), completed, null, succeeded ? completed : null, completed);
        return report with { Timings = timings, UploadSize = uploadSize, LocalEndPoint = report.LocalEndPoint ?? recorder.DatagramLocalEndPoint };
    }

    // A scheme whose handler times nothing itself reports the transfer's phases, as curl does for every scheme: pretransfer and starttransfer are the end of the transfer, which reads its first byte as it goes. HTTP times itself, and TFTP's unconnected socket reports no local end and no phases (measured, BL-515, BL-5493).
    private bool ReportsPhases(ITransferContext context) =>
        context.Url.Scheme is not ("http" or "https" or "tftp") && (recorder.FirstConnectTimings is not null || recorder.DatagramOpenedAt is not null);

    // The first connection's timings, else a UDP channel's: connected when it opened, within the transfer.
    private ConnectTimings? ConnectTimingsOf(long started, long completed)
    {
        if (recorder.FirstConnectTimings is not null || recorder.DatagramOpenedAt is null)
        {
            return recorder.FirstConnectTimings;
        }

        long opened = Math.Clamp(recorder.DatagramOpenedAt.Value, started, completed);
        return new ConnectTimings(started, opened, opened, null);
    }

    // The report's own upload size, else the bytes sent: none when a failure never connected.
    private long UploadSizeOf(TransferReport report, bool succeeded, long uploaded)
    {
        if (report.UploadSize > 0)
        {
            return report.UploadSize;
        }

        return !succeeded && recorder.FirstConnectTimings is null ? 0 : uploaded;
    }

    // The bytes the transfer sent as its body: the -d data when given, else what the upload stream gave. A failed non-HTTP transfer (a broker refusing the login before the publish) sent none of the -d data (BL-5492).
    private static long UploadedBy(ITransferContext context, long streamBytes, bool succeeded) =>
        context.PostData is { } postData && SendsPostDataEvenOnFailure(context, succeeded) ? postData.Length : streamBytes;

    private static bool SendsPostDataEvenOnFailure(ITransferContext context, bool succeeded) =>
        succeeded || context.Url.Scheme is "http" or "https";

    // The upload stream's position, or 0 when there is no upload or it cannot report one.
    private static long UploadPosition(ITransferContext context) =>
        context.Upload is { CanSeek: true } upload ? upload.Position : 0;
}
