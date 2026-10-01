using Curl.Protocol.Abstractions;

namespace Curl.Core.Fakes;

/// <summary>Records every info line a transfer reports; ignores the rest.</summary>
internal sealed class RecordingTransferEvents : ITransferEvents
{
    public List<string> Infos { get; } = [];

    public void ReportInfo(string text) => Infos.Add(text);

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
