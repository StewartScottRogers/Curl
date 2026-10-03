using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Passes every event on to <paramref name="inner" /> and writes the <c>[READ]</c> line curl 8.21.0
/// writes under <c>-v --trace-config read</c> (or <c>-vvv</c>, <c>all</c>) as a finished transfer resets
/// its client readers: <see cref="ResetLine" /> before the connection's <c>Connection #N to host
/// &lt;host&gt;:&lt;port&gt; left intact</c> or <c>shutting down connection #N</c> line, and not before a
/// failed transfer's <c>closing connection #N</c> (measured, BL-1159 Notes); and again after each followed
/// redirect's <c>Issue another request to this URL: '...'</c>, as the next hop starts (measured, BL-1189
/// Notes). After a followed redirect's <c>Need to rewind upload for next request</c> it writes
/// <see cref="NeedsRewindLine" /> before that line, <see cref="WillRewindLine" /> in place of the hop's
/// reset line and <see cref="RewindReadersLine" /> before its <c>Issue another request</c> (measured,
/// BL-1213 Notes). The runner writes the reset line once more itself, as the transfer starts.
/// </summary>
/// <param name="inner">The transfer's own events.</param>
internal sealed class ClientReaderResetTraceEvents(ITransferEvents inner) : ITransferEvents
{
    /// <summary>The line curl writes as a transfer resets its client readers.</summary>
    public const string ResetLine = "[READ] client_reset, clear readers";

    /// <summary>The line curl writes before <see cref="NeedToRewindUploadLine" /> (measured, BL-1213 Notes).</summary>
    public const string NeedsRewindLine = "[READ] client reader needs rewind before next request";

    /// <summary>The line curl writes in place of <see cref="ResetLine" /> when a hop's body is to be rewound (measured, BL-1213 Notes).</summary>
    public const string WillRewindLine = "[READ] client_reset, will rewind reader";

    /// <summary>The line curl writes before the next hop's <c>Issue another request</c> after <see cref="WillRewindLine" /> (measured, BL-1213 Notes).</summary>
    public const string RewindReadersLine = "[READ] client start, rewind readers";

    /// <summary>The plain <c>-v</c> line the HTTP handler writes after a followed redirect's status line when the request sent a body.</summary>
    private const string NeedToRewindUploadLine = "Need to rewind upload for next request";

    private const string ConnectionPrefix = "Connection #";
    private const string LeftIntactSuffix = " left intact";
    private const string ShuttingDownPrefix = "shutting down connection #";

    private bool rewinding;

    /// <inheritdoc />
    public void ReportInfo(string text)
    {
        if (LineBefore(text) is { } before)
        {
            inner.ReportInfo(before);
        }

        inner.ReportInfo(text);
        if (IssuesAnotherRequest(text))
        {
            inner.ReportInfo(ResetLine);
        }
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

    /// <summary>
    /// Gives the <c>[READ]</c> line curl writes before <paramref name="text" />, or <see langword="null" />,
    /// noting whether the hop's body is to be rewound.
    /// </summary>
    private string? LineBefore(string text)
    {
        if (text == NeedToRewindUploadLine)
        {
            rewinding = true;
            return NeedsRewindLine;
        }

        if (EndsTheTransfer(text))
        {
            return rewinding ? WillRewindLine : ResetLine;
        }

        if (rewinding && IssuesAnotherRequest(text))
        {
            rewinding = false;
            return RewindReadersLine;
        }

        return null;
    }

    private static bool EndsTheTransfer(string text) =>
        (text.StartsWith(ConnectionPrefix, StringComparison.Ordinal) && text.EndsWith(LeftIntactSuffix, StringComparison.Ordinal))
        || text.StartsWith(ShuttingDownPrefix, StringComparison.Ordinal);

    private static bool IssuesAnotherRequest(string text) =>
        text.StartsWith(RedirectFollower.IssueAnotherRequestMessagePrefix, StringComparison.Ordinal);
}
