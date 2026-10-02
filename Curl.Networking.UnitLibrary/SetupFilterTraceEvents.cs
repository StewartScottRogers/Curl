using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Passes every event on to <paramref name="inner" /> and adds the <c>[SETUP]</c> lines curl 8.21.0
/// writes for its connection setup filter under <c>-vv</c> and up, <c>--trace-config setup</c> or
/// <c>all</c> around a direct connect (measured, BL-1103 Notes): <c>[SETUP] happy eyeballing to origin
/// &lt;host&gt;:&lt;port&gt;</c> before the first <c>Trying</c> line, and the filter's removal after
/// <c>Established connection</c>. The <see cref="AddedLine" /> comes first, before any <c>[DNS]</c>
/// line, so the connector writes it before it builds the events below this one; a connect that fails
/// writes no more <c>[SETUP]</c> lines.
/// </summary>
/// <param name="inner">The events below the setup filter: the DNS filter's, or the transfer's own.</param>
/// <param name="host">The host the connection dials, after any <c>--connect-to</c> mapping.</param>
/// <param name="port">The port it dials.</param>
internal sealed class SetupFilterTraceEvents(ITransferEvents inner, string host, int port) : ITransferEvents
{
    /// <summary>The line curl writes as it adds the setup filter, before anything is resolved.</summary>
    public const string AddedLine = "[SETUP] added";

    private const string TryingPrefix = "  Trying ";

    private bool _eyeballing;

    /// <inheritdoc />
    public void ReportInfo(string text)
    {
        if (!_eyeballing && text.StartsWith(TryingPrefix, StringComparison.Ordinal))
        {
            _eyeballing = true;
            inner.ReportInfo($"[SETUP] happy eyeballing to origin {host}:{port}");
        }

        inner.ReportInfo(text);
    }

    /// <inheritdoc />
    public void ReportConnectionOpened(ConnectionOpenedEvent opened)
    {
        inner.ReportConnectionOpened(opened);
        inner.ReportInfo("[SETUP] removing connected setup filter");
        inner.ReportInfo("[SETUP] destroy");
    }

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
}
