using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ftp.Fakes;

/// <summary>
/// An <see cref="ITransferEvents" /> that records the name of every member called on it, in
/// order, with the text of each <see cref="ReportInfo" />, to show that a wrapper passes each
/// event through.
/// </summary>
public sealed class CallRecordingTransferEvents : ITransferEvents
{
    /// <summary>Gets every call, in order: the member's name, and for an info line <c>ReportInfo: &lt;text&gt;</c>.</summary>
    public List<string> Calls { get; } = [];

    /// <inheritdoc />
    public void ReportInfo(string text) => Calls.Add("ReportInfo: " + text);

    /// <inheritdoc />
    public void ReportConnectionOpened(ConnectionOpenedEvent opened) => Calls.Add(nameof(ReportConnectionOpened));

    /// <inheritdoc />
    public void ReportConnectionReused(ConnectionReusedEvent reused) => Calls.Add(nameof(ReportConnectionReused));

    /// <inheritdoc />
    public void ReportTlsHandshake(TlsHandshakeEvent handshake) => Calls.Add(nameof(ReportTlsHandshake));

    /// <inheritdoc />
    public void ReportTlsData(ReadOnlySpan<byte> bytes, bool sent) => Calls.Add(nameof(ReportTlsData));

    /// <inheritdoc />
    public void ReportTlsMessage(TlsMessageEvent message) => Calls.Add(nameof(ReportTlsMessage));

    /// <inheritdoc />
    public void ReportTlsTrust(TlsTrustEvent trust) => Calls.Add(nameof(ReportTlsTrust));

    /// <inheritdoc />
    public void ReportRequestHeader(ReadOnlySpan<byte> bytes) => Calls.Add(nameof(ReportRequestHeader));

    /// <inheritdoc />
    public void ReportResponseHeader(ReadOnlySpan<byte> bytes) => Calls.Add(nameof(ReportResponseHeader));

    /// <inheritdoc />
    public void ReportDataSent(ReadOnlySpan<byte> bytes) => Calls.Add(nameof(ReportDataSent));

    /// <inheritdoc />
    public void ReportDataReceived(ReadOnlySpan<byte> bytes) => Calls.Add(nameof(ReportDataReceived));
}
