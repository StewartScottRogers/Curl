using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Passes one HTTP transfer's progress to the context's <see cref="ITransferProgress" />, keeping
/// the sink's promises across the handler's retries (ADR-0065): "transfer started" is reported
/// once for the first connection, however many it opens, and once more for each request a
/// response asks for (<see cref="ReportRequestRetried" />), which starts its own meter line as a
/// redirect hop does; within one request a byte count below one already reported is dropped, so
/// a running total never goes backwards.
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
    /// Reports that a request a response asked for - an authentication retry or a resend without
    /// <c>Expect</c> - starts, as "request retried", and counts its bytes from zero: curl
    /// 8.21.0 draws each request of such a transfer on a progress-meter line of its own, as it
    /// draws each redirect hop (measured, BL-2087 Notes). Unlike a redirect hop, the line keeps the
    /// transfer's start time (measured, BL-2131 Notes).
    /// </summary>
    internal void ReportRequestRetried()
    {
        CountFromZero();
        sink.ReportRequestRetried();
    }

    /// <summary>
    /// Reports that a request whose pooled connection died before its response began is sent
    /// again on a fresh connection, as "request resent on a fresh connection", and counts its
    /// bytes from zero: curl 8.22.0 ends the dead attempt's meter line with one draw and starts a
    /// new one (measured, BL-2200 Notes).
    /// </summary>
    internal void ReportRequestResentOnFreshConnection()
    {
        CountFromZero();
        sink.ReportRequestResentOnFreshConnection();
    }

    private void CountFromZero()
    {
        started = true;
        downloaded = -1;
        uploaded = -1;
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
    /// <param name="bytesSoFar">The body bytes sent, chunk framing and the closing chunk included, as curl 8.21.0 counts them (measured, BL-2131 Notes).</param>
    /// <param name="expectedTotal">The body's length, or <see langword="null" /> when it is not known.</param>
    internal void ReportUploaded(long bytesSoFar, long? expectedTotal)
    {
        if (bytesSoFar >= uploaded)
        {
            uploaded = bytesSoFar;
            sink.ReportUploaded(bytesSoFar, expectedTotal);
        }
    }

    /// <summary>
    /// Reports that the final exchange's data is complete, before the connection-end <c>-v</c> line.
    /// </summary>
    internal void ReportTransferDone() => sink.ReportTransferDone();
}
