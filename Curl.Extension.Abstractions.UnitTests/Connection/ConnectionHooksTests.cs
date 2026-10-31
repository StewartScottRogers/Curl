namespace Curl.Extension.Abstractions.Connection;

[TestClass]
public sealed class ConnectionHooksTests
{
    private static readonly ExtensionOption Pipe = new("example-pipe", ExtensionOptionArgument.Value, "Connect through a named pipe");

    private static readonly ExtensionOption Tunnel = new("example-tunnel", ExtensionOptionArgument.None, "Connect through a tunnel");

    [TestMethod]
    public void ActivatedBy_WithNoExtensionOptionGiven_GivesCurlsConnectorsBack()
    {
        NamedConnector curls = new();
        NamedDatagramConnector curlsDatagrams = new();

        ConnectionHooks hooks = ConnectionHooks.ActivatedBy(ExtensionOptionValues.None, [ExampleHook.ActivatedBy(Pipe, new ExampleHook(Pipe))]);

        Assert.IsNull(hooks.Hook);
        Assert.AreSame(curls, hooks.ConnectorOver(curls));
        Assert.AreSame(curlsDatagrams, hooks.DatagramConnectorOver(curlsDatagrams));
        Assert.IsNull(ConnectionHooks.None.Hook);
        Assert.AreSame(curls, ConnectionHooks.None.ConnectorOver(curls));
        Assert.AreSame(curlsDatagrams, ConnectionHooks.None.DatagramConnectorOver(curlsDatagrams));
    }

    [TestMethod]
    public void ActivatedBy_WithItsOptionGiven_GivesTheHooksConnectorsOverCurls()
    {
        NamedConnector curls = new();
        NamedDatagramConnector curlsDatagrams = new();
        DatagramHook hook = new(Pipe);

        ConnectionHooks hooks = ConnectionHooks.ActivatedBy(ValuesOf(Pipe), [ExampleHook.ActivatedBy(Pipe, hook), ExampleHook.ActivatedBy(Tunnel, new ExampleHook(Tunnel))]);

        Assert.AreSame(hook, hooks.Hook);
        Assert.AreSame(hook.Connector, hooks.ConnectorOver(curls));
        Assert.AreSame(curls, hook.GivenConnector);
        Assert.AreSame(hook.DatagramConnector, hooks.DatagramConnectorOver(curlsDatagrams));
    }

    [TestMethod]
    public void DatagramConnectorOver_WithAHookSupplyingOnlyAConnector_GivesCurlsDatagramConnector()
    {
        NamedDatagramConnector curlsDatagrams = new();

        ConnectionHooks hooks = ConnectionHooks.ActivatedBy(ValuesOf(Pipe), [ExampleHook.ActivatedBy(Pipe, new ExampleHook(Pipe))]);

        Assert.AreSame(curlsDatagrams, hooks.DatagramConnectorOver(curlsDatagrams));
    }

    [TestMethod]
    public void ActivatedBy_WithTwoExtensionsClaimingTheConnection_FailsNamingBothOptions()
    {
        ArgumentException failure = Assert.ThrowsExactly<ArgumentException>(() => ConnectionHooks.ActivatedBy(
            ValuesOf(Pipe, Tunnel),
            [ExampleHook.ActivatedBy(Pipe, new ExampleHook(Pipe)), ExampleHook.ActivatedBy(Tunnel, new ExampleHook(Tunnel))]));

        Assert.AreEqual("values", failure.ParamName);
        StringAssert.StartsWith(failure.Message, "the options --example-pipe and --example-tunnel each supply the connection a transfer runs over; give only one of them");
    }

    [TestMethod]
    public void ConnectorOver_WithCurlsTlsProvider_GivesTheHookItOrCurlsConnectorWithoutAHook()
    {
        NamedConnector curls = new();
        NamedTlsProvider curlsTls = new();
        TlsLayeringHook layering = new(Pipe);

        ConnectionHooks hooks = ConnectionHooks.ActivatedBy(ValuesOf(Pipe), [_ => layering]);

        Assert.AreSame(layering.Connector, hooks.ConnectorOver(curls, curlsTls));
        Assert.AreSame(curlsTls, layering.GivenTlsProvider);
        Assert.AreSame(curls, ConnectionHooks.None.ConnectorOver(curls, curlsTls));
    }

    [TestMethod]
    public void ConnectorOver_WithCurlsTlsProviderAndAHookThatDoesNotLayerTls_GivesItsConnectorOverCurls()
    {
        NamedConnector curls = new();
        ExampleHook hook = new(Pipe);

        ConnectionHooks hooks = ConnectionHooks.ActivatedBy(ValuesOf(Pipe), [ExampleHook.ActivatedBy(Pipe, hook)]);

        Assert.AreSame(hook.Connector, hooks.ConnectorOver(curls, new NamedTlsProvider()));
        Assert.AreSame(curls, hook.GivenConnector);
    }

    private static ExtensionOptionValues ValuesOf(params ExtensionOption[] options) =>
        new(options.ToDictionary(option => option.Name, _ => ""));

    private class ExampleHook(ExtensionOption option) : IConnectionHook
    {
        public ExtensionOption Option => option;

        public NamedConnector Connector { get; } = new();

        public IConnector? GivenConnector { get; private set; }

        public static Func<ExtensionOptionValues, IConnectionHook?> ActivatedBy(ExtensionOption option, ExampleHook hook) =>
            values => values.IsGiven(option) ? hook : null;

        public IConnector ConnectorOver(IConnector curlConnector)
        {
            GivenConnector = curlConnector;
            return Connector;
        }
    }

    private sealed class DatagramHook(ExtensionOption option) : ExampleHook(option), IConnectionHook
    {
        public NamedDatagramConnector DatagramConnector { get; } = new();

        public IDatagramConnector DatagramConnectorOver(IDatagramConnector curlDatagramConnector) => DatagramConnector;
    }

    private sealed class TlsLayeringHook(ExtensionOption option) : ExampleHook(option), IConnectionHook
    {
        public ITlsProvider? GivenTlsProvider { get; private set; }

        public IConnector ConnectorOver(IConnector curlConnector, ITlsProvider curlTlsProvider)
        {
            GivenTlsProvider = curlTlsProvider;
            return Connector;
        }
    }

    private sealed class NamedTlsProvider : ITlsProvider
    {
        public ValueTask<ConnectResult> AuthenticateAsClientAsync(IConnection plaintext, string targetHost, CancellationToken cancellationToken) =>
            throw new NotSupportedException("never authenticates");
    }

    private sealed class NamedConnector : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            throw new NotSupportedException("never connects");
    }

    private sealed class NamedDatagramConnector : IDatagramConnector
    {
        public ValueTask<DatagramOpenResult> OpenAsync(string host, int port, CancellationToken cancellationToken) =>
            throw new NotSupportedException("never opens");
    }
}
