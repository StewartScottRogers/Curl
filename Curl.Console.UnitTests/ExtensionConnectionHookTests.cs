using System.Text;
using Curl.Cli;
using Curl.Engine;
using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Connection;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins that the Console passes the connection hook an option group's extension options activate down to the
/// engine (ADR-1010): a transfer with the option given runs over the hook's connector, two such options are a
/// command-line error with exit 2, and a transfer without one keeps curl's connector.
/// </summary>
[TestClass]
public sealed class ExtensionConnectionHookTests
{
    private static readonly ITlsProvider CurlTls = new UnusedTlsProvider();

    private static readonly ExtensionOption PipeOption = new("test-pipe", ExtensionOptionArgument.Value, "Test pipe");
    private static readonly ExtensionOption TunnelOption = new("test-tunnel", ExtensionOptionArgument.None, "Test tunnel");

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly ScriptedConnector curlServer = new([Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 4\r\n\r\ncurl")]);
    private readonly ScriptedConnector hookServer = new([Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 4\r\n\r\nhook")]);
    private readonly RecordingDatagramConnector curlDatagrams = new(CurlExitCode.CouldntConnect, "unused");

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    [TestMethod]
    public async Task RunAsync_ExtensionOptionGiven_TransfersOverTheExtensionsConnector()
    {
        int exitCode = await RunAsync("-sS", "--test-pipe", "p1", "http://127.0.0.1:20602/");

        Assert.AreEqual(0, exitCode, StandardErrorText);
        Assert.AreEqual("hook", Encoding.UTF8.GetString(standardOutput.ToArray()));
        Assert.Contains("GET / HTTP/1.1\r\n", Encoding.Latin1.GetString(hookServer.Written));
        Assert.IsEmpty(curlServer.Written);
    }

    [TestMethod]
    public async Task RunAsync_TwoConnectionHookOptionsGiven_ExitsTwoNamingBothOptions()
    {
        int exitCode = await RunAsync("-sS", "--test-pipe", "p1", "--test-tunnel", "http://127.0.0.1:20602/");

        Assert.AreEqual(2, exitCode);
        Assert.AreEqual(
            "curl: the options --test-pipe and --test-tunnel each supply the connection a transfer runs over; give only one of them\n"
                + "curl: try 'curl --help' or 'curl --manual' for more information\n",
            StandardErrorText.ReplaceLineEndings("\n"));
        Assert.AreEqual(0, standardOutput.Length);
        Assert.IsEmpty(curlServer.Written);
        Assert.IsEmpty(hookServer.Written);
    }

    [TestMethod]
    public async Task RunAsync_NoExtensionOptionGiven_TransfersOverCurlsConnector()
    {
        int exitCode = await RunAsync("-sS", "http://127.0.0.1:20602/");

        Assert.AreEqual(0, exitCode, StandardErrorText);
        Assert.AreEqual("curl", Encoding.UTF8.GetString(standardOutput.ToArray()));
        Assert.IsEmpty(hookServer.Written);
    }

    [TestMethod]
    public void ExtensionConnectionHooksOf_NoExtensionOptionGiven_PassesNoConnectorToTheEngine()
    {
        ConnectionHooks hooks = CurlComposition.ExtensionConnectionHooksOf(new CommandLineOptions(), [ActivatedByPipe, ActivatedByTunnel]);

        Assert.IsNull(hooks.Hook);
        Assert.IsNull(CurlComposition.ExtensionConnectorOf(hooks, CurlTls));
        Assert.IsNull(CurlComposition.ExtensionDatagramConnectorOf(hooks));
        Assert.IsNull(CurlComposition.ExtensionConnectorOf(null, CurlTls));
        Assert.IsNull(CurlComposition.ExtensionDatagramConnectorOf(null));
        Assert.IsNull(CurlComposition.ExtensionConnectionHooksOf(new CommandLineOptions()).Hook);
    }

    [TestMethod]
    public void ExtensionDatagramConnectorOf_HookActivated_GivesTheHooksDatagramConnector()
    {
        ConnectionHooks hooks = CurlComposition.ExtensionConnectionHooksOf(new CommandLineOptions(), [_ => new ConnectorReplacingHook(TunnelOption, hookServer)]);

        Assert.AreSame(curlDatagrams, CurlComposition.ExtensionDatagramConnectorOf(hooks)!(curlDatagrams));
        Assert.AreSame(hookServer, CurlComposition.ExtensionConnectorOf(hooks, CurlTls)!(curlServer));
    }

    [TestMethod]
    public void ExtensionConnectorOf_HookActivated_HandsTheHookTheGroupsTlsProvider()
    {
        TlsProviderRecordingHook hook = new(TunnelOption, hookServer);
        ConnectionHooks hooks = CurlComposition.ExtensionConnectionHooksOf(new CommandLineOptions(), [_ => hook]);

        IConnector connector = CurlComposition.ExtensionConnectorOf(hooks, CurlTls)!(curlServer);

        Assert.AreSame(hookServer, connector);
        Assert.AreSame(CurlTls, hook.GivenTlsProvider);
    }

    private IConnectionHook? ActivatedByPipe(ExtensionOptionValues values) =>
        values.ValueOf(PipeOption) is not null ? new ConnectorReplacingHook(PipeOption, hookServer) : null;

    private IConnectionHook? ActivatedByTunnel(ExtensionOptionValues values) =>
        values.ValueOf(TunnelOption) is not null ? new ConnectorReplacingHook(TunnelOption, hookServer) : null;

    private Task<int> RunAsync(params string[] arguments) =>
        CurlComposition.CreateRunner(
                standardOutput,
                standardError,
                new MemoryStream(),
                curlServer,
                curlDatagrams,
                extensionOptions: [PipeOption, TunnelOption],
                extensionConnectionHookActivators: [ActivatedByPipe, ActivatedByTunnel])
            .RunAsync(arguments);

    private sealed class ConnectorReplacingHook(ExtensionOption option, IConnector connector) : IConnectionHook
    {
        public ExtensionOption Option => option;

        public IConnector ConnectorOver(IConnector curlConnector) => connector;
    }

    private sealed class TlsProviderRecordingHook(ExtensionOption option, IConnector connector) : IConnectionHook
    {
        public ExtensionOption Option => option;

        public ITlsProvider? GivenTlsProvider { get; private set; }

        public IConnector ConnectorOver(IConnector curlConnector) => connector;

        public IConnector ConnectorOver(IConnector curlConnector, ITlsProvider curlTlsProvider)
        {
            GivenTlsProvider = curlTlsProvider;
            return connector;
        }
    }

    private sealed class UnusedTlsProvider : ITlsProvider
    {
        public ValueTask<ConnectResult> AuthenticateAsClientAsync(IConnection plaintext, string targetHost, CancellationToken cancellationToken) =>
            throw new NotSupportedException("never authenticates");
    }
}
