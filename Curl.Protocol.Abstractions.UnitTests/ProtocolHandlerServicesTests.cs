namespace Curl.Protocol.Abstractions;

/// <summary>Checks the services the engine hands extension handlers (ADR-0999), with the host's process starter (ADR-1184).</summary>
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

    [TestMethod]
    public void Constructor_WithoutAProcessStarter_CarriesNone() =>
        Assert.IsNull(new ProtocolHandlerServices(null!, null!, null!, null!).ProcessStarter);

    [TestMethod]
    public void Constructor_WithAProcessStarter_CarriesIt()
    {
        NoProcessStarter starter = new();

        ProtocolHandlerServices services = new(null!, null!, null!, null!, starter);

        Assert.AreSame(starter, services.ProcessStarter);
    }

    [TestMethod]
    public void Constructor_WithoutAnEnvironmentReader_CarriesNone() =>
        Assert.IsNull(new ProtocolHandlerServices(null!, null!, null!, null!).ReadEnvironmentVariable);

    [TestMethod]
    public void Constructor_WithAnEnvironmentReader_CarriesIt()
    {
        Func<string, string?> reader = _ => "value";

        ProtocolHandlerServices services = new(null!, null!, null!, null!, ReadEnvironmentVariable: reader);

        Assert.AreSame(reader, services.ReadEnvironmentVariable);
    }

    private sealed class NoConnector : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class NoProcessStarter : IExtensionProcessStarter
    {
        public string? ReadEnvironmentVariable(string name) => throw new NotSupportedException();

        public IExtensionProcess Start(string program, IReadOnlyList<string> arguments) => throw new NotSupportedException();
    }
}
