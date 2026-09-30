using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// Keeps the <c>-v</c> lines reported to it, in order, so they can be written later where
/// curl 8.21.0 writes them: the authenticator reports a Negotiate context's failure while the
/// first request's <c>Authorization</c> value is made, before the connection is opened, and
/// curl writes it just before the request (measured, BL-843 Notes). Every other event is
/// dropped, as an authenticator reports none.
/// </summary>
internal sealed class HttpInfoLineRecorder : ITransferEvents
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
