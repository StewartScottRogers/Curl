using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Passes every event on to <paramref name="inner" /> unchanged and keeps the certificate verify codes the
/// transfer reports, the origin's and an HTTPS proxy's, as <c>Curl.Console</c>'s
/// <c>TlsResultRecordingTransferEvents</c> keeps them for <c>%{ssl_verify_result}</c> and
/// <c>%{proxy_ssl_verify_result}</c> (BL-661); <see cref="ReportOn" /> puts them on the finished
/// transfer's <see cref="TransferReport" />, for <c>CURLINFO_SSL_VERIFYRESULT</c> and
/// <c>CURLINFO_PROXY_SSL_VERIFYRESULT</c> (BL-2366).
/// </summary>
/// <param name="inner">The transfer's own events.</param>
internal sealed class TlsVerifyResultRecordingTransferEvents(ITransferEvents inner) : ITransferEvents
{
    /// <summary>Gets the origin's last reported verify code; <c>0</c> until one is reported.</summary>
    internal long SslVerifyResult { get; private set; }

    /// <summary>Gets the HTTPS proxy's last reported verify code; <c>0</c> until one is reported.</summary>
    internal long ProxySslVerifyResult { get; private set; }

    /// <summary>
    /// The finished transfer with the recorded verify codes on its report: unchanged when neither was
    /// reported or when the progress callback aborted the transfer, else on its report, or on a new one when
    /// the handler returned none.
    /// </summary>
    /// <param name="result">The finished transfer, or <see langword="null" /> when the progress callback aborted it.</param>
    /// <returns>The transfer with the verify codes on its report, or <see langword="null" /> for an aborted one.</returns>
    internal TransferResult? ReportOn(TransferResult? result) =>
        result is null || (SslVerifyResult == 0 && ProxySslVerifyResult == 0)
            ? result
            : result with
            {
                Report = (result.Report ?? new TransferReport()) with
                {
                    SslVerifyResult = SslVerifyResult,
                    ProxySslVerifyResult = ProxySslVerifyResult,
                },
            };

    /// <inheritdoc />
    public void ReportCertificateVerifyResult(long verifyResult, bool isProxy)
    {
        if (isProxy)
        {
            ProxySslVerifyResult = verifyResult;
        }
        else
        {
            SslVerifyResult = verifyResult;
        }

        inner.ReportCertificateVerifyResult(verifyResult, isProxy);
    }

    /// <inheritdoc />
    public void ReportTlsEarlyData(long bytes) => inner.ReportTlsEarlyData(bytes);

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
    public void ReportRequestHeader(ReadOnlySpan<byte> bytes) => inner.ReportRequestHeader(bytes);

    /// <inheritdoc />
    public void ReportResponseHeader(ReadOnlySpan<byte> bytes) => inner.ReportResponseHeader(bytes);

    /// <inheritdoc />
    public void ReportDataSent(ReadOnlySpan<byte> bytes) => inner.ReportDataSent(bytes);

    /// <inheritdoc />
    public void ReportDataReceived(ReadOnlySpan<byte> bytes) => inner.ReportDataReceived(bytes);
}
