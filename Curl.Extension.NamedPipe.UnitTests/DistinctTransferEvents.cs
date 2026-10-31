using Curl.Protocol.Abstractions;

namespace Curl.Extension.NamedPipe;

/// <summary>Transfer events a test can tell apart from <see cref="NoTransferEvents.Instance" />; they keep nothing.</summary>
internal sealed class DistinctTransferEvents : ITransferEvents
{
    public void ReportInfo(string text)
    {
    }

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
