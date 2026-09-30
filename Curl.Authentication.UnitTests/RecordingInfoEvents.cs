using Curl.Protocol.Abstractions;

namespace Curl.Authentication;

/// <summary>
/// An <see cref="ITransferEvents" /> that records the <see cref="ReportInfo" /> lines it is
/// given, in order, and ignores every other event.
/// </summary>
internal sealed class RecordingInfoEvents : ITransferEvents
{
    public List<string> Info { get; } = [];

    public void ReportInfo(string text) => Info.Add(text);

    public void ReportConnectionOpened(ConnectionOpenedEvent opened)
    {
    }

    public void ReportConnectionReused(ConnectionReusedEvent reused)
    {
    }

    public void ReportTlsHandshake(TlsHandshakeEvent handshake)
    {
    }

    public void ReportTlsData(ReadOnlySpan<byte> bytes, bool sent)
    {
    }

    public void ReportRequestHeader(ReadOnlySpan<byte> bytes)
    {
    }

    public void ReportResponseHeader(ReadOnlySpan<byte> bytes)
    {
    }

    public void ReportDataSent(ReadOnlySpan<byte> bytes)
    {
    }

    public void ReportDataReceived(ReadOnlySpan<byte> bytes)
    {
    }
}
