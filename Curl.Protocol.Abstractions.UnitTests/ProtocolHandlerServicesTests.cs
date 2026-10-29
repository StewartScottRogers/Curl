namespace Curl.Protocol.Abstractions;

[TestClass]
public sealed class ProtocolHandlerServicesTests
{
    [TestMethod]
    public void Constructor_KeepsEachTransportAsGiven()
    {
        IConnector connector = new NoConnector();
        ProtocolHandlerServices services = new(connector, null!, null!, null!);

        Assert.AreSame(connector, services.Connector);
        Assert.IsNull(services.DatagramConnector);
        Assert.IsNull(services.TlsProvider);
        Assert.IsNull(services.DnsResolver);
    }

    private sealed class NoConnector : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
