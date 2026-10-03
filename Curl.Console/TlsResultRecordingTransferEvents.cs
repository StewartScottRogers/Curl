using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Passes every event on to <paramref name="inner" /> unchanged and records the TLS results
/// <c>-w</c> prints on <paramref name="state" />: each reported certificate verify code, the
/// origin's as <see cref="RunningTransferState.SslVerifyResult" /> and an HTTPS proxy's as
/// <see cref="RunningTransferState.ProxySslVerifyResult" /> (BL-661), and the TLS 1.3 early data
/// bytes sent as <see cref="RunningTransferState.TlsEarlyDataSent" /> (BL-1150).
/// </summary>
/// <param name="inner">The transfer's own events.</param>
/// <param name="state">The running transfer the results belong to.</param>
internal sealed class TlsResultRecordingTransferEvents(ITransferEvents inner, RunningTransferState state) : ITransferEvents
{
    /// <inheritdoc />
    public void ReportTlsEarlyData(long bytes)
    {
        state.TlsEarlyDataSent = bytes;
        inner.ReportTlsEarlyData(bytes);
    }

    /// <inheritdoc />
    public void ReportCertificateVerifyResult(long verifyResult, bool isProxy)
    {
        if (isProxy)
        {
            state.ProxySslVerifyResult = verifyResult;
        }
        else
        {
            state.SslVerifyResult = verifyResult;
        }

        inner.ReportCertificateVerifyResult(verifyResult, isProxy);
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
    public void ReportRequestHeader(ReadOnlySpan<byte> bytes) => inner.ReportRequestHeader(bytes);

    /// <inheritdoc />
    public void ReportResponseHeader(ReadOnlySpan<byte> bytes) => inner.ReportResponseHeader(bytes);

    /// <inheritdoc />
    public void ReportDataSent(ReadOnlySpan<byte> bytes) => inner.ReportDataSent(bytes);

    /// <inheritdoc />
    public void ReportDataReceived(ReadOnlySpan<byte> bytes) => inner.ReportDataReceived(bytes);
}
