using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ssh.Fakes;

/// <summary>
/// An <see cref="ITransferEvents" /> that records each information line with how many SFTP
/// requests the connection had been written when it was reported, as
/// <c>"&lt;count&gt;: &lt;line&gt;"</c>, so a test can pin where a line falls among the
/// requests. Every other event is ignored.
/// </summary>
/// <param name="connection">The connection whose written SFTP requests are counted.</param>
public sealed class RequestCountingTransferEvents(ScriptedConnection connection) : ITransferEvents
{
    /// <summary>Gets every information line, each prefixed with the requests written before it.</summary>
    public List<string> Lines { get; } = [];

    /// <inheritdoc />
    public void ReportInfo(string text) => Lines.Add($"{SftpServerScript.SftpRequests(connection.Written).Count}: {text}");

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
