using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Coap.Client;

/// <summary>Records the info lines and opened connections a transfer reports, in order, and ignores every other event.</summary>
internal sealed class RecordingTransferEvents : ITransferEvents
{
    /// <summary>Gets each info line reported.</summary>
    public List<string> InfoLines { get; } = [];

    /// <summary>Gets each opened connection reported.</summary>
    public List<ConnectionOpenedEvent> OpenedConnections { get; } = [];

    /// <inheritdoc />
    public void ReportInfo(string text) => InfoLines.Add(text);

    /// <inheritdoc />
    public void ReportConnectionOpened(ConnectionOpenedEvent opened) => OpenedConnections.Add(opened);

    /// <inheritdoc />
    public void ReportConnectionReused(ConnectionReusedEvent reused)
    {
    }

    /// <inheritdoc />
    public void ReportTlsHandshake(TlsHandshakeEvent handshake)
    {
    }

    /// <inheritdoc />
    public void ReportTlsData(ReadOnlySpan<byte> bytes, bool sent)
    {
    }

    /// <inheritdoc />
    public void ReportRequestHeader(ReadOnlySpan<byte> bytes)
    {
    }

    /// <inheritdoc />
    public void ReportResponseHeader(ReadOnlySpan<byte> bytes)
    {
    }

    /// <inheritdoc />
    public void ReportDataSent(ReadOnlySpan<byte> bytes)
    {
    }

    /// <inheritdoc />
    public void ReportDataReceived(ReadOnlySpan<byte> bytes)
    {
    }
}
