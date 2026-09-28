using Curl.Protocol.Abstractions;

namespace Curl.Networking.Fakes;

/// <summary>
/// An <see cref="ITransferEvents" /> that records the information lines, open and reuse events
/// and TLS handshakes it is given and ignores the rest.
/// </summary>
public sealed class RecordingTransferEvents : ITransferEvents
{
    /// <summary>Gets every <see cref="ReportInfo" /> text, in order.</summary>
    public List<string> Info { get; } = [];

    /// <summary>Gets every <see cref="ReportConnectionReused" /> event, in order.</summary>
    public List<ConnectionReusedEvent> Reused { get; } = [];

    /// <summary>Gets every <see cref="ReportTlsHandshake" /> event, in order.</summary>
    public List<TlsHandshakeEvent> Handshakes { get; } = [];

    /// <inheritdoc />
    public void ReportInfo(string text) => Info.Add(text);

    /// <summary>Gets every <see cref="ReportConnectionOpened" /> event, in order.</summary>
    public List<ConnectionOpenedEvent> Opened { get; } = [];

    /// <inheritdoc />
    public void ReportConnectionOpened(ConnectionOpenedEvent opened) => Opened.Add(opened);

    /// <inheritdoc />
    public void ReportConnectionReused(ConnectionReusedEvent reused) => Reused.Add(reused);

    /// <inheritdoc />
    public void ReportTlsHandshake(TlsHandshakeEvent handshake) => Handshakes.Add(handshake);

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
