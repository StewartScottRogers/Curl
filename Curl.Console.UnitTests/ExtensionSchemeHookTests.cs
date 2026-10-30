using System.Text;
using Curl.Cli;
using Curl.Engine;
using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Scheme;
using Curl.Extension.Abstractions.Transfer;
using Curl.Networking;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins that the Console passes the scheme hook an option group's extension options activate down to the engine
/// (ADR-1048): with the option given the hook's handler serves its schemes and curl's handlers the rest, two such
/// options are a command-line error with exit 2, and without one curl's handler serves every scheme.
/// </summary>
[TestClass]
public sealed class ExtensionSchemeHookTests
{
    private static readonly ExtensionOption ConnectOption = new("test-connect", ExtensionOptionArgument.None, "Test extended CONNECT");
    private static readonly ExtensionOption RelayOption = new("test-relay", ExtensionOptionArgument.Value, "Test relay");

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly ScriptedConnector curlServer = new([Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 4\r\n\r\ncurl")]);
    private readonly RecordingDatagramConnector curlDatagrams = new(CurlExitCode.CouldntConnect, "unused");

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    [TestMethod]
    public async Task RunAsync_ExtensionOptionGiven_ServesWsWithTheExtensionsHandler()
    {
        int exitCode = await RunAsync("-sS", "--test-connect", "ws://127.0.0.1:20603/chat");

        Assert.AreEqual(0, exitCode, StandardErrorText);
        Assert.AreEqual("hook ws", Encoding.UTF8.GetString(standardOutput.ToArray()));
        Assert.IsEmpty(curlServer.Written);
    }

    [TestMethod]
    public async Task RunAsync_ExtensionOptionGiven_ServesOtherSchemesWithCurlsHandler()
    {
        int exitCode = await RunAsync("-sS", "--test-connect", "http://127.0.0.1:20603/");

        Assert.AreEqual(0, exitCode, StandardErrorText);
        Assert.AreEqual("curl", Encoding.UTF8.GetString(standardOutput.ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_NoExtensionOptionGiven_ServesWsWithCurlsHandler()
    {
        await RunAsync("-sS", "ws://127.0.0.1:20603/chat");

        Assert.Contains("Upgrade: websocket\r\n", Encoding.Latin1.GetString(curlServer.Written));
        Assert.DoesNotContain("hook", Encoding.UTF8.GetString(standardOutput.ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_TwoSchemeHookOptionsGiven_ExitsTwoNamingBothOptions()
    {
        int exitCode = await RunAsync("-sS", "--test-connect", "--test-relay", "r1", "ws://127.0.0.1:20603/chat");

        Assert.AreEqual(2, exitCode);
        Assert.AreEqual(
            "curl: the options --test-connect and --test-relay each serve a curl scheme in place of curl's handler; give only one of them\n"
                + "curl: try 'curl --help' or 'curl --manual' for more information\n",
            StandardErrorText.ReplaceLineEndings("\n"));
        Assert.AreEqual(0, standardOutput.Length);
        Assert.IsEmpty(curlServer.Written);
    }

    [TestMethod]
    public async Task ExtensionHandlerHooksOf_SchemeAndTransferHooksActivated_RunsTheTransferHookAroundTheExtensionsHandler()
    {
        FinishCountingHook transferHook = new();

        Func<IProtocolHandler, IProtocolHandler> hooks = CurlComposition.ExtensionHandlerHooksOf(new CommandLineOptions(), [_ => transferHook], [_ => new WsServingHook(ConnectOption)], curlServer, new TcpConnectionListener())!;
        TransferResult result = await hooks(new UnusedHandler()).ExecuteAsync(new TransferContext { Url = CurlUrl.Parse("wss://example.com/"), Output = new MemoryStream() });

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(1, transferHook.FinishedTransfers);
    }

    [TestMethod]
    public void ExtensionHandlerHooksOf_NoExtensionOptionGiven_PassesNoHooksToTheEngine()
    {
        Assert.IsNull(CurlComposition.ExtensionHandlerHooksOf(new CommandLineOptions(), [], [ActivatedByConnect, ActivatedByRelay], curlServer, new TcpConnectionListener()));
        Assert.IsNull(CurlComposition.ExtensionHandlerHooksOf(new CommandLineOptions(), curlServer, new TcpConnectionListener()));
    }

    private static ISchemeHook? ActivatedByConnect(ExtensionOptionValues values) =>
        values.IsGiven(ConnectOption) ? new WsServingHook(ConnectOption) : null;

    private static ISchemeHook? ActivatedByRelay(ExtensionOptionValues values) =>
        values.IsGiven(RelayOption) ? new WsServingHook(RelayOption) : null;

    private Task<int> RunAsync(params string[] arguments) =>
        CurlComposition.CreateRunner(
                standardOutput,
                standardError,
                new MemoryStream(),
                curlServer,
                curlDatagrams,
                extensionOptions: [ConnectOption, RelayOption],
                extensionSchemeHookActivators: [ActivatedByConnect, ActivatedByRelay])
            .RunAsync(arguments);

    private sealed class WsServingHook(ExtensionOption option) : ISchemeHook
    {
        public ExtensionOption Option => option;

        public IReadOnlyCollection<string> Schemes { get; } = ["ws", "wss"];

        public IProtocolHandler HandlerOver(IProtocolHandler curlHandler, TransferHookServices services) => new UrlEchoingHandler();
    }

    private sealed class UrlEchoingHandler : IProtocolHandler
    {
        public IReadOnlyCollection<string> SupportedSchemes { get; } = ["ws", "wss"];

        public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
        {
            byte[] body = Encoding.UTF8.GetBytes($"hook {context.Url.Scheme}");
            await context.Output.WriteAsync(body, context.CancellationToken);
            return TransferResult.Success(body.Length);
        }
    }

    private sealed class UnusedHandler : IProtocolHandler
    {
        public IReadOnlyCollection<string> SupportedSchemes { get; } = ["ws", "wss"];

        public ValueTask<TransferResult> ExecuteAsync(ITransferContext context) =>
            throw new AssertFailedException("curl's handler ran.");
    }

    private sealed class FinishCountingHook : ITransferHook
    {
        public int FinishedTransfers { get; private set; }

        public void TransferFinished(TransferResult result) => FinishedTransfers++;
    }
}
