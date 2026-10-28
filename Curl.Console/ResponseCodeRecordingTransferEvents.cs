using System.Globalization;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Passes every event on to <paramref name="inner" /> unchanged and records, as
/// <see cref="RunningTransferState.ReceivedResponseCode" />, the code of the last response status line
/// received, so that a <c>-Z</c> transfer <c>--fail-early</c> aborts after its response arrived still
/// prints it as <c>%{http_code}</c>, as curl 8.22.0 does (BL-3702, CGR-173): the handler that was
/// cancelled returns no report to read it from.
/// </summary>
/// <param name="inner">The transfer's own events.</param>
/// <param name="state">The running transfer the code belongs to.</param>
internal sealed class ResponseCodeRecordingTransferEvents(ITransferEvents inner, RunningTransferState state) : ITransferEvents
{
    /// <summary>The length of a status line's three-digit code.</summary>
    private const int CodeLength = 3;

    /// <inheritdoc />
    public void ReportResponseHeader(ReadOnlySpan<byte> bytes)
    {
        if (StatusCodeOf(bytes) is { } code)
        {
            state.ReceivedResponseCode = code;
        }

        inner.ReportResponseHeader(bytes);
    }

    /// <inheritdoc />
    public void ReportInfo(string text) => inner.ReportInfo(text);

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
    public void ReportWarning(string text) => inner.ReportWarning(text);

    /// <inheritdoc />
    public void ReportCertificateVerifyResult(long verifyResult, bool isProxy) => inner.ReportCertificateVerifyResult(verifyResult, isProxy);

    /// <inheritdoc />
    public void ReportTlsEarlyData(long bytes) => inner.ReportTlsEarlyData(bytes);

    /// <inheritdoc />
    public void ReportRequestHeader(ReadOnlySpan<byte> bytes) => inner.ReportRequestHeader(bytes);

    /// <inheritdoc />
    public void ReportDataSent(ReadOnlySpan<byte> bytes) => inner.ReportDataSent(bytes);

    /// <inheritdoc />
    public void ReportDataReceived(ReadOnlySpan<byte> bytes) => inner.ReportDataReceived(bytes);

    /// <summary>
    /// Gives the code of a status line such as <c>HTTP/1.1 200 OK</c> or <c>HTTP/2 200</c>: the three
    /// digits after the first space of a line that starts with <c>HTTP/</c>.
    /// </summary>
    /// <param name="line">The response header line.</param>
    /// <returns>The code, or <see langword="null" /> when the line is not a status line.</returns>
    internal static int? StatusCodeOf(ReadOnlySpan<byte> line)
    {
        int space = line.IndexOf((byte)' ');
        return line.StartsWith("HTTP/"u8)
            && space > 0
            && line.Length >= space + 1 + CodeLength
            && int.TryParse(line.Slice(space + 1, CodeLength), NumberStyles.None, CultureInfo.InvariantCulture, out int code)
            ? code
            : null;
    }
}
