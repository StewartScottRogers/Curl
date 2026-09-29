using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ldap.Fakes;

/// <summary>
/// An <see cref="ITransferEvents" /> that records the information lines and the sizes of the
/// received data it is given, in one list in order, and ignores the rest.
/// </summary>
public sealed class RecordingTransferEvents : ITransferEvents
{
    /// <summary>Gets each <see cref="ReportInfo" /> text, and <c>{ [N bytes data]</c> for each <see cref="ReportDataReceived" />, in order.</summary>
    public List<string> Lines { get; } = [];

    /// <inheritdoc />
    public void ReportInfo(string text) => Lines.Add(text);

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
    public void ReportDataReceived(ReadOnlySpan<byte> bytes) => Lines.Add($"{{ [{bytes.Length} bytes data]");
}
