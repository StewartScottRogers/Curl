using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http.Fakes;

/// <summary>
/// An <see cref="ITransferEvents" /> that records the information lines and the header and
/// data events it is given, and ignores the connection and TLS events.
/// </summary>
public sealed class RecordingTransferEvents : ITransferEvents
{
    /// <summary>Gets every <see cref="ReportInfo" /> text, in order.</summary>
    public List<string> Info { get; } = [];

    /// <summary>
    /// Gets every recorded event, in order, as <c>-v</c> marks them: <c>* </c> and the info
    /// text, <c>&gt; </c> and <c>&lt; </c> and the header bytes, <c>} </c> and <c>{ </c> and the
    /// data bytes, the bytes as Latin-1 text.
    /// </summary>
    public List<string> Events { get; } = [];

    /// <inheritdoc />
    public void ReportInfo(string text)
    {
        Info.Add(text);
        Events.Add("* " + text);
    }

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
    public void ReportRequestHeader(ReadOnlySpan<byte> bytes) => Events.Add("> " + Encoding.Latin1.GetString(bytes));

    /// <inheritdoc />
    public void ReportResponseHeader(ReadOnlySpan<byte> bytes) => Events.Add("< " + Encoding.Latin1.GetString(bytes));

    /// <inheritdoc />
    public void ReportDataSent(ReadOnlySpan<byte> bytes) => Events.Add("} " + Encoding.Latin1.GetString(bytes));

    /// <inheritdoc />
    public void ReportDataReceived(ReadOnlySpan<byte> bytes) => Events.Add("{ " + Encoding.Latin1.GetString(bytes));
}
