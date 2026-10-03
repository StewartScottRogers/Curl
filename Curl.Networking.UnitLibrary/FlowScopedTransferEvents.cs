using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Passes every event on to the transfer events <see cref="Current" /> holds for the asynchronous
/// flow that reports it, or drops it when that flow has none. It lets a piece built once per run,
/// before any transfer exists, report to whichever transfer is using it: the run's
/// <see cref="DohDnsResolver" /> writes its <c>--trace-config doh</c> lines here, and
/// <see cref="TcpConnector" /> sets <see cref="Current" /> to the resolving transfer's events
/// before it asks the resolver (BL-1102), so serial and <c>-Z</c> transfers each get their own.
/// </summary>
public sealed class FlowScopedTransferEvents : ITransferEvents
{
    private readonly AsyncLocal<ITransferEvents?> _current = new();

    /// <summary>
    /// Gets or sets the events of the transfer the current asynchronous flow works for; a value set
    /// in an <see langword="async" /> method reaches what it awaits and is gone once it returns.
    /// </summary>
    public ITransferEvents? Current
    {
        get => _current.Value;
        set => _current.Value = value;
    }

    private ITransferEvents Target => _current.Value ?? NoTransferEvents.Instance;

    /// <inheritdoc />
    public void ReportInfo(string text) => Target.ReportInfo(text);

    /// <inheritdoc />
    public void ReportConnectionOpened(ConnectionOpenedEvent opened) => Target.ReportConnectionOpened(opened);

    /// <inheritdoc />
    public void ReportConnectionReused(ConnectionReusedEvent reused) => Target.ReportConnectionReused(reused);

    /// <inheritdoc />
    public void ReportTlsHandshake(TlsHandshakeEvent handshake) => Target.ReportTlsHandshake(handshake);

    /// <inheritdoc />
    public void ReportTlsData(ReadOnlySpan<byte> bytes, bool sent) => Target.ReportTlsData(bytes, sent);

    /// <inheritdoc />
    public void ReportTlsMessage(TlsMessageEvent message) => Target.ReportTlsMessage(message);

    /// <inheritdoc />
    public void ReportTlsTrust(TlsTrustEvent trust) => Target.ReportTlsTrust(trust);

    /// <inheritdoc />
    public void ReportCertificateVerifyResult(long verifyResult, bool isProxy) =>
        Target.ReportCertificateVerifyResult(verifyResult, isProxy);

    /// <inheritdoc />
    public void ReportTlsEarlyData(long bytes) => Target.ReportTlsEarlyData(bytes);

    /// <inheritdoc />
    public void ReportRequestHeader(ReadOnlySpan<byte> bytes) => Target.ReportRequestHeader(bytes);

    /// <inheritdoc />
    public void ReportResponseHeader(ReadOnlySpan<byte> bytes) => Target.ReportResponseHeader(bytes);

    /// <inheritdoc />
    public void ReportDataSent(ReadOnlySpan<byte> bytes) => Target.ReportDataSent(bytes);

    /// <inheritdoc />
    public void ReportDataReceived(ReadOnlySpan<byte> bytes) => Target.ReportDataReceived(bytes);
}
