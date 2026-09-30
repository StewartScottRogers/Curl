using Curl.Protocol.Abstractions;

namespace Curl.Networking.Fakes;

/// <summary>
/// An <see cref="IConnectReplyHeadWritingEvents" /> that records every CONNECT reply head it
/// is given, as Latin-1 text, and ignores every other event.
/// </summary>
public sealed class HeadRecordingTransferEvents : IConnectReplyHeadWritingEvents
{
    /// <summary>Gets every <see cref="WriteConnectReplyHeadAsync" /> head, in order.</summary>
    public List<string> Heads { get; } = [];

    /// <inheritdoc />
    public ValueTask WriteConnectReplyHeadAsync(ReadOnlyMemory<byte> head, CancellationToken cancellationToken)
    {
        Heads.Add(System.Text.Encoding.Latin1.GetString(head.Span));
        return ValueTask.CompletedTask;
    }

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
