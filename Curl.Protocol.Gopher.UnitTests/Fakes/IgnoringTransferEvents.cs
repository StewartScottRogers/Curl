using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Gopher.Fakes;

/// <summary>
/// An <see cref="ITransferEvents" /> that ignores every event, for a test that only
/// checks which sink a handler hands on.
/// </summary>
public sealed class IgnoringTransferEvents : ITransferEvents
{
    /// <inheritdoc />
    public void ReportInfo(string text)
    {
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
