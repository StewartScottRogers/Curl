using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Passes every event on to <paramref name="inner" /> unchanged and keeps the first
/// <c>InitializeSecurityContext failed: ...</c> line a Negotiate step reports. curl 8.22.0's SSPI build writes
/// that line with <c>failf</c>, and libcurl's error buffer keeps a transfer's first <c>failf</c>, so under
/// <c>CURLAUTH_ANY</c> or <c>CURLAUTH_ANYSAFE</c> with no Kerberos credentials the transfer goes on to another
/// scheme, returns <c>CURLE_OK</c> and leaves that line in <c>CURLOPT_ERRORBUFFER</c> (BL-3322);
/// <see cref="ReportOn" /> puts it on a successful result. The GSS-API build writes its line with <c>infof</c>, so
/// it never matches and the error buffer stays empty there.
/// </summary>
/// <param name="inner">The transfer's own events.</param>
internal sealed class NegotiateFailureRecordingTransferEvents(ITransferEvents inner) : ITransferEvents
{
    /// <summary>What curl's SSPI build writes before the SSPI status of a failed context step.</summary>
    internal const string SspiFailurePrefix = "InitializeSecurityContext failed: ";

    /// <summary>Gets the first SSPI failure line reported, or <see langword="null" /> when none was.</summary>
    internal string? FirstSspiFailure { get; private set; }

    /// <summary>
    /// The finished transfer after a recorded failure: Negotiate as the scheme picked when no scheme was
    /// (libcurl picks Negotiate first and keeps it for <c>CURLINFO_HTTPAUTH_USED</c> when its step fails), and the
    /// recorded line as the error message when the transfer succeeded without one, as libcurl leaves a
    /// <c>failf</c> text in the error buffer of a transfer that goes on to succeed. Unchanged when none was recorded.
    /// </summary>
    /// <param name="result">The finished transfer, or <see langword="null" /> when a callback aborted it.</param>
    /// <returns>The transfer with those changes, or <paramref name="result" /> as it is.</returns>
    internal TransferResult? ReportOn(TransferResult? result) =>
        FirstSspiFailure is not null && result is not null
            ? result with { ErrorMessage = MessageAfterFailure(result), Report = WithNegotiatePicked(result.Report) }
            : result;

    private string? MessageAfterFailure(TransferResult result) =>
        result is { ExitCode: CurlExitCode.Ok, ErrorMessage: null or "" } ? FirstSspiFailure : result.ErrorMessage;

    private static TransferReport WithNegotiatePicked(TransferReport? report) =>
        report is { AuthSchemePicked: not HttpAuthSchemes.None }
            ? report
            : (report ?? new TransferReport()) with { AuthSchemePicked = HttpAuthSchemes.Negotiate };

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
    public void ReportWarning(string text) => inner.ReportWarning(text);

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
