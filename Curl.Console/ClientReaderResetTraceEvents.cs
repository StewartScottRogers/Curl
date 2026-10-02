using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Passes every event on to <paramref name="inner" /> and writes the <c>[READ]</c> line curl 8.21.0
/// writes under <c>-v --trace-config read</c> (or <c>-vvv</c>, <c>all</c>) as a finished transfer resets
/// its client readers: <see cref="ResetLine" /> before the connection's <c>Connection #N to host
/// &lt;host&gt;:&lt;port&gt; left intact</c> or <c>shutting down connection #N</c> line, and not before a
/// failed transfer's <c>closing connection #N</c> (measured, BL-1159 Notes). The runner writes the same
/// line once more itself, as the transfer starts.
/// </summary>
/// <param name="inner">The transfer's own events.</param>
internal sealed class ClientReaderResetTraceEvents(ITransferEvents inner) : ITransferEvents
{
    /// <summary>The line curl writes as a transfer resets its client readers.</summary>
    public const string ResetLine = "[READ] client_reset, clear readers";

    private const string ConnectionPrefix = "Connection #";
    private const string LeftIntactSuffix = " left intact";
    private const string ShuttingDownPrefix = "shutting down connection #";

    /// <inheritdoc />
    public void ReportInfo(string text)
    {
        if (EndsTheTransfer(text))
        {
            inner.ReportInfo(ResetLine);
        }

        inner.ReportInfo(text);
    }

    /// <inheritdoc />
    public void ReportConnectionOpened(ConnectionOpenedEvent opened) => inner.ReportConnectionOpened(opened);

    /// <inheritdoc />
    public void ReportConnectionReused(ConnectionReusedEvent reused) => inner.ReportConnectionReused(reused);

    /// <inheritdoc />
    public void ReportTlsHandshake(TlsHandshakeEvent handshake) => inner.ReportTlsHandshake(handshake);

    /// <inheritdoc />
    public void ReportTlsData(ReadOnlySpan<byte> bytes, bool sent) => inner.ReportTlsData(bytes, sent);

    /// <inheritdoc />
    public void ReportTlsMessage(TlsMessageEvent message) => inner.ReportTlsMessage(message);

    /// <inheritdoc />
    public void ReportTlsTrust(TlsTrustEvent trust) => inner.ReportTlsTrust(trust);

    /// <inheritdoc />
    public void ReportCertificateVerifyResult(long verifyResult, bool isProxy) =>
        inner.ReportCertificateVerifyResult(verifyResult, isProxy);

    /// <inheritdoc />
    public void ReportTlsEarlyData(long bytes) => inner.ReportTlsEarlyData(bytes);

    /// <inheritdoc />
    public void ReportRequestHeader(ReadOnlySpan<byte> bytes) => inner.ReportRequestHeader(bytes);

    /// <inheritdoc />
    public void ReportResponseHeader(ReadOnlySpan<byte> bytes) => inner.ReportResponseHeader(bytes);

    /// <inheritdoc />
    public void ReportDataSent(ReadOnlySpan<byte> bytes) => inner.ReportDataSent(bytes);

    /// <inheritdoc />
    public void ReportDataReceived(ReadOnlySpan<byte> bytes) => inner.ReportDataReceived(bytes);

    private static bool EndsTheTransfer(string text) =>
        (text.StartsWith(ConnectionPrefix, StringComparison.Ordinal) && text.EndsWith(LeftIntactSuffix, StringComparison.Ordinal))
        || text.StartsWith(ShuttingDownPrefix, StringComparison.Ordinal);
}
