namespace Curl.Protocol.Abstractions;

/// <summary>
/// An <see cref="ITransferEvents" /> that is not <see cref="NoTransferEvents.Instance" />,
/// for tests that pin a sink is passed through as given. Its members are never called.
/// </summary>
internal sealed class StubTransferEvents : ITransferEvents
{
    public void ReportInfo(string text) => throw new NotSupportedException();

    public void ReportConnectionOpened(ConnectionOpenedEvent opened) => throw new NotSupportedException();

    public void ReportConnectionReused(ConnectionReusedEvent reused) => throw new NotSupportedException();

    public void ReportTlsHandshake(TlsHandshakeEvent handshake) => throw new NotSupportedException();

    public void ReportTlsData(ReadOnlySpan<byte> bytes, bool sent) => throw new NotSupportedException();

    public void ReportRequestHeader(ReadOnlySpan<byte> bytes) => throw new NotSupportedException();

    public void ReportResponseHeader(ReadOnlySpan<byte> bytes) => throw new NotSupportedException();

    public void ReportDataSent(ReadOnlySpan<byte> bytes) => throw new NotSupportedException();

    public void ReportDataReceived(ReadOnlySpan<byte> bytes) => throw new NotSupportedException();
}
