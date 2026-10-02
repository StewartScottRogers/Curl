using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.File.Fakes;

/// <summary>
/// An <see cref="ITransferEvents" /> that records every event it is given, in order, as
/// one line of <see cref="Transcript" />, and the information lines alone in
/// <see cref="Info" />.
/// </summary>
public sealed class RecordingTransferEvents : ITransferEvents
{
    /// <summary>Gets every <see cref="ReportInfo" /> text, in order.</summary>
    public List<string> Info { get; } = [];

    /// <summary>
    /// Gets every event, in order: an information line as <c>* text</c>, received data as
    /// <c>&lt;= </c> and its bytes read as ASCII, sent data as <c>=&gt; </c> and its bytes,
    /// and every other event as its method's name.
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
        Transcript.Add(nameof(ReportConnectionOpened));

    /// <inheritdoc />
    public void ReportConnectionReused(ConnectionReusedEvent reused) =>
        Transcript.Add(nameof(ReportConnectionReused));

    /// <inheritdoc />
    public void ReportTlsHandshake(TlsHandshakeEvent handshake) =>
        Transcript.Add(nameof(ReportTlsHandshake));

    /// <inheritdoc />
    public void ReportTlsData(ReadOnlySpan<byte> bytes, bool sent) =>
        Transcript.Add(nameof(ReportTlsData));

    /// <inheritdoc />
    public void ReportRequestHeader(ReadOnlySpan<byte> bytes) =>
        Transcript.Add(nameof(ReportRequestHeader));

    /// <inheritdoc />
    public void ReportResponseHeader(ReadOnlySpan<byte> bytes) =>
        Transcript.Add(nameof(ReportResponseHeader));

    /// <inheritdoc />
    public void ReportDataSent(ReadOnlySpan<byte> bytes) =>
        Transcript.Add("=> " + Encoding.ASCII.GetString(bytes));

    /// <inheritdoc />
    public void ReportDataReceived(ReadOnlySpan<byte> bytes) =>
        Transcript.Add("<= " + Encoding.ASCII.GetString(bytes));
}
