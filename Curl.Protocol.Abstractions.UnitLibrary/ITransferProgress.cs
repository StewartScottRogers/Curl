namespace Curl.Protocol.Abstractions;

/// <summary>
/// Where a protocol handler reports how far a transfer has got, for <c>Curl.Console</c>'s
/// progress meter to draw (ADR-0045).
/// </summary>
/// <remarks>
/// Every member is synchronous and cheap, because handlers call the byte members from
/// inside their copy loops. No member carries a timestamp and no implementation in a
/// protocol library reads a clock: the consumer measures rate and time on its injected
/// <see cref="TimeProvider" /> when each report arrives. No handler is obliged to report
/// bytes; the counters of one that never does stay at zero. <see cref="NoTransferProgress" />
/// is the sink used when nobody is listening.
/// </remarks>
public interface ITransferProgress
{
    /// <summary>
    /// Reports that the transfer is past connect or open: a connection is established, or
    /// the source is open. Called at most once per transfer, before the handler can fail
    /// past that point; the consumer ignores a repeat.
    /// </summary>
    void ReportTransferStarted();

    /// <summary>
    /// Reports the body bytes received so far in this transfer.
    /// </summary>
    /// <param name="bytesSoFar">The running total of body bytes received, never a delta and never decreasing.</param>
    /// <param name="expectedTotal">The body size the handler expects to receive, or <see langword="null" /> when it is not known.</param>
    void ReportDownloaded(long bytesSoFar, long? expectedTotal);

    /// <summary>
    /// Reports the body bytes sent so far in this transfer.
    /// </summary>
    /// <param name="bytesSoFar">The running total of body bytes sent, never a delta and never decreasing.</param>
    /// <param name="expectedTotal">The size of the upload, or <see langword="null" /> when it is not known.</param>
    void ReportUploaded(long bytesSoFar, long? expectedTotal);

    /// <summary>
    /// Reports that the transfer's data is complete and the handler is about to report what
    /// became of its connection, so the consumer can finish drawing before the connection-end
    /// <c>-v</c> line (ADR-0045, ADR-0111). Called at most once per handler run, for
    /// the final exchange only, whether that exchange succeeded or failed; the default body
    /// does nothing, so a handler that never calls it and a sink that ignores it need no change.
    /// </summary>
    void ReportTransferDone()
    {
    }
}
