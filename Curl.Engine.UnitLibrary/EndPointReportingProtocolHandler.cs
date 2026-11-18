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

        result = recorder.ReportOn(result, handlerCountsConnections: result.Report is not null && context.Url.Scheme is "http" or "https");
        return StampTimingsAndUploadSize(result, context, started, UploadedBy(context, UploadPosition(context) - uploadStart));
    }

    // Gives a report with no timings the transfer's connect and total times and, when it sent none itself, the bytes the upload stream gave (BL-5410). A failure with no report stays without one (BL-5421), and one that never connected sent nothing.
    private TransferResult StampTimingsAndUploadSize(TransferResult result, ITransferContext context, long started, long uploaded)
    {
        if (result.Report is null && !result.IsSuccess)
        {
            return result;
        }

        TransferReport report = result.Report ?? new TransferReport { DownloadSize = result.BytesTransferred };
        if (report.Timings is not null)
        {
            return result;
        }

        TransferTimings timings = new(started, recorder.FirstConnectTimings, null, null, null, context.TimeProvider.GetTimestamp());
        return result with { Report = report with { Timings = timings, UploadSize = UploadSizeOf(report, result.IsSuccess, uploaded) } };
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

    // The bytes the transfer sent as its body: the -d data when given, else what the upload stream gave.
    private static long UploadedBy(ITransferContext context, long streamBytes) =>
        context.PostData is { } postData ? postData.Length : streamBytes;

    // The upload stream's position, or 0 when there is no upload or it cannot report one.
    private static long UploadPosition(ITransferContext context) =>
        context.Upload is { CanSeek: true } upload ? upload.Position : 0;
}
