using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ssh.Fakes;

/// <summary>
/// An <see cref="ITransferEvents" /> that records, in order, the information lines and the
/// data blocks it is given: an information line prefixed <c>"* "</c>, data received
/// <c>"&lt;= "</c> and data sent <c>"=&gt; "</c>, as Latin-1 text. Connect, TLS and header
/// events, which no SSH transfer reports, are ignored.
/// </summary>
public sealed class TranscriptTransferEvents : ITransferEvents
{
    /// <summary>Gets every recorded event, in the order reported.</summary>
    public List<string> Transcript { get; } = [];

    /// <inheritdoc />
    public void ReportInfo(string text) => Transcript.Add("* " + text);

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
    public void ReportRequestHeader(ReadOnlySpan<byte> bytes)
    {
    }

    /// <inheritdoc />
    public void ReportResponseHeader(ReadOnlySpan<byte> bytes)
    {
    }

    /// <inheritdoc />
    public void ReportDataSent(ReadOnlySpan<byte> bytes) => Transcript.Add("=> " + Encoding.Latin1.GetString(bytes));

    /// <inheritdoc />
    public void ReportDataReceived(ReadOnlySpan<byte> bytes) => Transcript.Add("<= " + Encoding.Latin1.GetString(bytes));
}
