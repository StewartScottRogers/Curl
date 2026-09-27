using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// Passes one HTTP transfer's progress to the context's <see cref="ITransferProgress" />, keeping
/// the sink's promises across the handler's retries (ADR-0065): "transfer started" is reported at
/// most once, however many connections the transfer opens, and a byte count below one already
/// reported is dropped, so a body sent again for an authentication retry or a resend without
/// <c>Expect</c> never makes a running total go backwards.
/// </summary>
/// <param name="sink">Where the reports go.</param>
internal sealed class HttpTransferProgress(ITransferProgress sink)
{
    private bool started;

    private long downloaded = -1;

    private long uploaded = -1;

    /// <summary>
    /// Gets a reporter whose reports go nowhere, for a body that is read and discarded.
    /// </summary>
    internal static HttpTransferProgress Silent => new(NoTransferProgress.Instance);

    /// <summary>
    /// Reports that a connection is established, the first time only.
    /// </summary>
    internal void ReportTransferStarted()
    {
        if (!started)
        {
            started = true;
            sink.ReportTransferStarted();
        }
    }

    /// <summary>
    /// Reports the response body bytes received so far, unless a larger count was reported already.
    /// </summary>
    /// <param name="bytesSoFar">The body bytes the output has accepted.</param>
    /// <param name="expectedTotal">The body's Content-Length, or <see langword="null" /> when it has none.</param>
    internal void ReportDownloaded(long bytesSoFar, long? expectedTotal)
    {
        if (bytesSoFar >= downloaded)
        {
            downloaded = bytesSoFar;
            sink.ReportDownloaded(bytesSoFar, expectedTotal);
        }
    }

    /// <summary>
    /// Reports the request body bytes sent so far, unless a larger count was reported already.
    /// </summary>
    /// <param name="bytesSoFar">The body bytes sent, chunk framing excluded.</param>
    /// <param name="expectedTotal">The body's length, or <see langword="null" /> when it is not known.</param>
    internal void ReportUploaded(long bytesSoFar, long? expectedTotal)
    {
        if (bytesSoFar >= uploaded)
        {
            uploaded = bytesSoFar;
            sink.ReportUploaded(bytesSoFar, expectedTotal);
        }
    }
}
