using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Passes every event on to <paramref name="inner" /> unchanged and keeps the first
/// <c>InitializeSecurityContext failed: ...</c> line, so a CONNECT tunnel the proxy then refuses
/// fails with it (BL-1033). curl 8.21.0's SSPI build writes that line with <c>failf</c>, and
/// libcurl's error buffer keeps a transfer's first <c>failf</c>, so it, not
/// <c>CONNECT tunnel failed, response 407</c>, is the message; the GSS-API build writes its
/// <c>gss_init_sec_context() failed: ...</c> with <c>infof</c>, so that line is not kept.
/// </summary>
/// <param name="inner">The transfer's own events.</param>
internal sealed class SspiFailureRecordingTransferEvents(ITransferEvents inner) : ITransferEvents
{
    /// <summary>What curl's SSPI build writes before the SSPI status of a failed context step.</summary>
    internal const string SspiFailurePrefix = "InitializeSecurityContext failed: ";

    /// <summary>Gets the first SSPI failure line reported, or <see langword="null" /> when none was.</summary>
    public string? FirstSspiFailure { get; private set; }

    /// <inheritdoc />
    public void ReportInfo(string text)
    {
        if (FirstSspiFailure is null && text.StartsWith(SspiFailurePrefix, StringComparison.Ordinal))
        {
            FirstSspiFailure = text;
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
}
