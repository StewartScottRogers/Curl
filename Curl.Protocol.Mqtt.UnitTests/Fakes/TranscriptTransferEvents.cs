using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Mqtt.Fakes;

/// <summary>
/// An <see cref="ITransferEvents" /> that records, in order, the information lines and the
/// data blocks it is given, as Latin-1 text: an information line prefixed <c>"* "</c>,
/// data sent <c>"=&gt; "</c> and data received <c>"&lt;= "</c>. Connect and TLS events,
/// which the connector reports, are ignored.
/// </summary>
public sealed class TranscriptTransferEvents : ITransferEvents
{
    /// <summary>Gets every recorded event, in the order reported.</summary>
    public List<string> Transcript { get; } = [];

    /// <summary>Gets or sets what runs after each event is recorded, or <see langword="null" /> for nothing.</summary>
    public Action? Recorded { get; set; }

    /// <inheritdoc />
    public void ReportInfo(string text) => Record("* " + text);

    /// <inheritdoc />
    public void ReportConnectionOpened(ConnectionOpenedEvent opened)
    {
    }

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
    public void ReportRequestHeader(ReadOnlySpan<byte> bytes) => Record("> " + Encoding.Latin1.GetString(bytes));

    /// <inheritdoc />
    public void ReportResponseHeader(ReadOnlySpan<byte> bytes) => Record("< " + Encoding.Latin1.GetString(bytes));

    /// <inheritdoc />
    public void ReportDataSent(ReadOnlySpan<byte> bytes) => Record("=> " + Encoding.Latin1.GetString(bytes));

    /// <inheritdoc />
    public void ReportDataReceived(ReadOnlySpan<byte> bytes) => Record("<= " + Encoding.Latin1.GetString(bytes));

    private void Record(string line)
    {
        Transcript.Add(line);
        Recorded?.Invoke();
    }
}
