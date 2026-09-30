using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ws;

/// <summary>
/// Keeps the <c>-v</c> lines the authenticator reports, in order, so the WebSocket handler can
/// write them where curl 8.21.0 does and fail a refused upgrade with the first of them
/// (BL-955): a Negotiate context's failure is written before <c>Server auth using Negotiate</c>
/// and, for a 401, just before the <c>WWW-Authenticate</c> header offering Negotiate. Every
/// other event is dropped, as an authenticator reports none.
/// </summary>
internal sealed class WsInfoLineRecorder : ITransferEvents
{
    private readonly List<string> lines = [];

    /// <summary>Gets the lines reported, in the order they were reported.</summary>
    internal IReadOnlyList<string> Lines => lines;

    /// <inheritdoc />
    public void ReportInfo(string text) => lines.Add(text);

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
    public void ReportDataSent(ReadOnlySpan<byte> bytes)
    {
    }

    /// <inheritdoc />
    public void ReportDataReceived(ReadOnlySpan<byte> bytes)
    {
    }
}
