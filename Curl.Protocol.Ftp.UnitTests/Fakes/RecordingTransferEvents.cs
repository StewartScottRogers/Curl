using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ftp.Fakes;

/// <summary>
/// An <see cref="ITransferEvents" /> that records the information lines and headers it
/// is given and ignores the rest.
/// </summary>
public sealed class RecordingTransferEvents : ITransferEvents
{
    /// <summary>Gets every <see cref="ReportInfo" /> text, in order.</summary>
    public List<string> Info { get; } = [];

    /// <inheritdoc />
    public void ReportInfo(string text) => Info.Add(text);

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
    public void ReportRequestHeader(ReadOnlySpan<byte> bytes) => Headers.Add("> " + Encoding.Latin1.GetString(bytes));

    /// <inheritdoc />
    public void ReportResponseHeader(ReadOnlySpan<byte> bytes) => Headers.Add("< " + Encoding.Latin1.GetString(bytes));

    /// <inheritdoc />
    public void ReportDataSent(ReadOnlySpan<byte> bytes)
    {
    }

    /// <inheritdoc />
    public void ReportDataReceived(ReadOnlySpan<byte> bytes)
    {
    }
}
