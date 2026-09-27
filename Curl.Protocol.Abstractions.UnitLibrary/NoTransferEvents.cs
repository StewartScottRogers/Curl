namespace Curl.Protocol.Abstractions;

/// <summary>
/// The <see cref="ITransferEvents" /> used when nobody is listening: every member does
/// nothing (ADR-0046).
/// </summary>
public sealed class NoTransferEvents : ITransferEvents
{
    private NoTransferEvents()
    {
    }

    /// <summary>
    /// Gets the one instance.
    /// </summary>
    public static NoTransferEvents Instance { get; } = new();

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
