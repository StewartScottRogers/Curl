using System.Globalization;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Passes every event on to <paramref name="inner" /> and writes the <c>[WRITE]</c> lines curl 8.21.0
/// writes under <c>-v --trace-config write</c> (or <c>-vvv</c>, <c>all</c>) as its client writers take the
/// response on (measured, BL-1187 Notes): four lines after each response header line, the status line's
/// of type <c>c</c> and every later one's of type <c>4</c> after a <c>header_collect pushed</c> line; five
/// after each block of body bytes, ending in <c>xfer_write_resp</c>; and <see cref="DoneLine" /> before the
/// connection's <c>left intact</c> or <c>shutting down connection</c> line.
/// </summary>
/// <remarks>
/// curl's <c>xfer_write_resp</c> length is the bytes of the one read it handed on. Curl's handler reports
/// header lines and body blocks, not reads, so the length here is every header byte reported since the
/// last <c>xfer_write_resp</c> plus the body block's bytes, which is curl's number whenever the head and
/// the first body block arrive in one read (ADR-0357 amendment, BL-1187). A response with no body writes
/// its <c>xfer_write_resp</c> for the head alone, before <see cref="DoneLine" />.
/// </remarks>
/// <param name="inner">The transfer's own events.</param>
internal sealed class ClientWriterTraceEvents(ITransferEvents inner) : ITransferEvents
{
    /// <summary>The line curl writes as the client writers finish a transfer's response.</summary>
    public const string DoneLine = "[WRITE] [OUT] done";

    private const string ConnectionPrefix = "Connection #";
    private const string LeftIntactSuffix = " left intact";
    private const string ShuttingDownPrefix = "shutting down connection #";

    private bool nextHeaderIsStatusLine = true;
    private bool responseStarted;
    private int bytesSinceLastResponseWrite;

    /// <inheritdoc />
    public void ReportInfo(string text)
    {
        if (responseStarted && EndsTheTransfer(text))
        {
            if (bytesSinceLastResponseWrite > 0)
            {
                WriteResponseWritten(0);
            }

            inner.ReportInfo(DoneLine);
            responseStarted = false;
            nextHeaderIsStatusLine = true;
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
    public void ReportResponseHeader(ReadOnlySpan<byte> bytes)
    {
        inner.ReportResponseHeader(bytes);
        int length = bytes.Length;
        string type = "c";
        if (!nextHeaderIsStatusLine)
        {
            type = "4";
            inner.ReportInfo(Invariant($"[WRITE] header_collect pushed(type=1, len={length}) -> 0"));
        }

        inner.ReportInfo(Invariant($"[WRITE] [OUT] wrote {length} header bytes -> {length}"));
        WriteDownloadLines(type, "header", length);
        responseStarted = true;
        nextHeaderIsStatusLine = bytes.SequenceEqual("\r\n"u8);
        bytesSinceLastResponseWrite += length;
    }

    /// <inheritdoc />
    public void ReportDataSent(ReadOnlySpan<byte> bytes) => inner.ReportDataSent(bytes);

    /// <inheritdoc />
    public void ReportDataReceived(ReadOnlySpan<byte> bytes)
    {
        inner.ReportDataReceived(bytes);
        int length = bytes.Length;
        inner.ReportInfo(Invariant($"[WRITE] [OUT] wrote {length} body bytes -> {length}"));
        WriteDownloadLines("1", "body", length);
        responseStarted = true;
        WriteResponseWritten(length);
    }

    private void WriteDownloadLines(string type, string kind, int length)
    {
        inner.ReportInfo(Invariant($"[WRITE] [PAUSE] writing {length}/{length} bytes of type {type} -> 0"));
        inner.ReportInfo(Invariant($"[WRITE] download_write {kind}(type={type}, blen={length}) -> 0"));
        inner.ReportInfo(Invariant($"[WRITE] client_write(type={type}, len={length}) -> 0"));
    }

    private void WriteResponseWritten(int bodyLength)
    {
        inner.ReportInfo(Invariant($"[WRITE] xfer_write_resp(len={bytesSinceLastResponseWrite + bodyLength}, eos=0) -> 0"));
        bytesSinceLastResponseWrite = 0;
    }

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

    private static bool EndsTheTransfer(string text) =>
        (text.StartsWith(ConnectionPrefix, StringComparison.Ordinal) && text.EndsWith(LeftIntactSuffix, StringComparison.Ordinal))
        || text.StartsWith(ShuttingDownPrefix, StringComparison.Ordinal);
}
