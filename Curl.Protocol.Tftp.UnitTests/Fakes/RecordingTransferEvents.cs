using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Tftp.Fakes;

/// <summary>
/// An <see cref="ITransferEvents" /> that records every event it is given, in order, as one
/// string each: an info line as its text, data received as <c>&lt;= </c> and its bytes,
/// data sent as <c>=&gt; </c> and its bytes (Latin-1), anything else as the member's name.
/// </summary>
public sealed class RecordingTransferEvents : ITransferEvents
{
    /// <summary>Gets every event reported, in order.</summary>
    public List<string> Steps { get; } = [];

    /// <inheritdoc />
    public void ReportInfo(string text) => Steps.Add(text);

    /// <inheritdoc />
    public void ReportConnectionOpened(ConnectionOpenedEvent opened) => Steps.Add(nameof(ReportConnectionOpened));

    /// <inheritdoc />
    public void ReportConnectionReused(ConnectionReusedEvent reused) => Steps.Add(nameof(ReportConnectionReused));

    /// <inheritdoc />
    public void ReportTlsHandshake(TlsHandshakeEvent handshake) => Steps.Add(nameof(ReportTlsHandshake));

    /// <inheritdoc />
    public void ReportTlsData(ReadOnlySpan<byte> bytes, bool sent) => Steps.Add(nameof(ReportTlsData));

    /// <inheritdoc />
    public void ReportRequestHeader(ReadOnlySpan<byte> bytes) => Steps.Add(nameof(ReportRequestHeader));

    /// <inheritdoc />
    public void ReportResponseHeader(ReadOnlySpan<byte> bytes) => Steps.Add(nameof(ReportResponseHeader));

    /// <inheritdoc />
    public void ReportDataSent(ReadOnlySpan<byte> bytes) => Steps.Add("=> " + Encoding.Latin1.GetString(bytes));

    /// <inheritdoc />
    public void ReportDataReceived(ReadOnlySpan<byte> bytes) => Steps.Add("<= " + Encoding.Latin1.GetString(bytes));
}
