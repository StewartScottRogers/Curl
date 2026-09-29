using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ftp.Fakes;

/// <summary>
/// An <see cref="ITransferEvents" /> that records the information lines, headers and data
/// it is given, each on its own and all together in order, and ignores the rest.
/// </summary>
public sealed class RecordingTransferEvents : ITransferEvents
{
    /// <summary>Gets every <see cref="ReportInfo" /> text, in order.</summary>
    public List<string> Info { get; } = [];

    /// <summary>
    /// Gets every information line, header and data block reported, in order, as Latin-1
    /// text: an information line prefixed <c>"* "</c>, a header as in <see cref="Headers" />,
    /// data received <c>"&lt;= "</c> and data sent <c>"=&gt; "</c>.
    /// </summary>
    public List<string> Transcript { get; } = [];

    /// <summary>Gets every data block received, in order, as Latin-1 text.</summary>
    public List<string> DataReceived { get; } = [];

    /// <summary>Gets every data block sent, in order, as Latin-1 text.</summary>
    public List<string> DataSent { get; } = [];

    /// <inheritdoc />
    public void ReportInfo(string text)
    {
        Info.Add(text);
        Transcript.Add("* " + text);
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

    /// <summary>
    /// Gets every header reported, in order, as Latin-1 text: a request header prefixed
    /// <c>"> "</c> and a response header <c>"< "</c>, each with its line end as reported.
    /// </summary>
    public List<string> Headers { get; } = [];

    /// <inheritdoc />
    public void ReportRequestHeader(ReadOnlySpan<byte> bytes) => AddHeader("> " + Encoding.Latin1.GetString(bytes));

    /// <inheritdoc />
    public void ReportResponseHeader(ReadOnlySpan<byte> bytes) => AddHeader("< " + Encoding.Latin1.GetString(bytes));

    /// <inheritdoc />
    public void ReportDataSent(ReadOnlySpan<byte> bytes)
    {
        string text = Encoding.Latin1.GetString(bytes);
        DataSent.Add(text);
        Transcript.Add("=> " + text);
    }

    /// <inheritdoc />
    public void ReportDataReceived(ReadOnlySpan<byte> bytes)
    {
        string text = Encoding.Latin1.GetString(bytes);
        DataReceived.Add(text);
        Transcript.Add("<= " + text);
    }

    private void AddHeader(string header)
    {
        Headers.Add(header);
        Transcript.Add(header);
    }
}
