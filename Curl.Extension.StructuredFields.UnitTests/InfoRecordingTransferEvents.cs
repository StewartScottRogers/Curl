namespace Curl.Extension.StructuredFields.UnitTests;

/// <summary>Transfer events that keep every informational line reported and ignore the rest.</summary>
internal sealed class InfoRecordingTransferEvents : ITransferEvents
{
    public List<string> InfoLines { get; } = [];

    public void ReportInfo(string text) => InfoLines.Add(text);

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
