using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Imap.Fakes;

/// <summary>
/// An <see cref="ITransferEvents" /> that records the information lines it is given, and
/// every line, header and data event in order as <see cref="Transcript" />.
/// </summary>
public sealed class RecordingTransferEvents : ITransferEvents
{
    /// <summary>Gets every <see cref="ReportInfo" /> text, in order.</summary>
    public List<string> Info { get; } = [];

    /// <summary>
    /// Gets every event in order: <c>* text</c> for an information line, <c>&gt; </c> and
    /// <c>&lt; </c> with the Latin-1 bytes of a request and a response header, and
    /// <c>} N</c> and <c>{ N</c> with the byte count of data sent and received, and
    /// <c>+ opened #N to host</c> for a connection opened.
    /// </summary>
    public List<string> Transcript { get; } = [];

    /// <inheritdoc />
    public void ReportInfo(string text)
    {
        Info.Add(text);
        Transcript.Add("* " + text);
    }

    /// <inheritdoc />
    public void ReportConnectionOpened(ConnectionOpenedEvent opened) =>
        Transcript.Add($"+ opened #{opened.ConnectionNumber} to {opened.HostName}");

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
    public void ReportRequestHeader(ReadOnlySpan<byte> bytes) => Transcript.Add("> " + Encoding.Latin1.GetString(bytes));

    /// <inheritdoc />
    public void ReportResponseHeader(ReadOnlySpan<byte> bytes) => Transcript.Add("< " + Encoding.Latin1.GetString(bytes));

    /// <inheritdoc />
    public void ReportDataSent(ReadOnlySpan<byte> bytes) => Transcript.Add("} " + bytes.Length);

    /// <inheritdoc />
    public void ReportDataReceived(ReadOnlySpan<byte> bytes) => Transcript.Add("{ " + bytes.Length);
}
