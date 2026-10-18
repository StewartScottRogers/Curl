namespace Curl.Api;

public sealed partial record CurlRequest
{
    /// <summary>
    /// Takes a <see cref="CurlTransferProgress"/> each time the engine reports the transfer's progress
    /// (BL-2505, ADR-0643), on the thread the engine calls from: a <see cref="Progress{T}"/> posts it to
    /// its own synchronization context. Reports stop once the transfer ends. A report that throws aborts
    /// the transfer, which then fails with <see cref="Errors.CurlException"/> (<c>CURLE_ABORTED_BY_CALLBACK</c>).
    /// Default <see langword="null"/>: no reports.
    /// </summary>
    public IProgress<CurlTransferProgress>? Progress { get; init; }
}
