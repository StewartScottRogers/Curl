using System.Text.Json;
using Curl.Mcp.Protocol.JsonRpc;
using Curl.Mcp.Protocol.Transport;

namespace Curl.Mcp.Protocol.Server;

[TestClass]
public sealed class McpServerTests
{
    private const string InitializeLine = """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"{0}","capabilities":{},"clientInfo":{"name":"golden","version":"1.0"}}}""";

    private const string GoldenInitializeResponse = """{"result":{"protocolVersion":"{0}","capabilities":{"logging":{},"resources":{},"tools":{"listChanged":true}},"serverInfo":{"name":"Curl.Mcp","version":"0.1.0.0"}},"id":1,"jsonrpc":"2.0"}""";

    private static readonly McpServerInfo ServerInfo = new("Curl.Mcp", "0.1.0.0");

    // The capabilities the SDK 2.2.0 server answered with in the golden files of BL-3374.
    private static readonly IMcpMethodHandler[] GoldenHandlers =
    [
        new StubCapabilityHandler("logging/setLevel", "logging", "{}"),
        new StubCapabilityHandler("resources/list", "resources", "{}"),
        new StubCapabilityHandler("tools/list", "tools", """{"listChanged":true}"""),
    ];

    private static (FakeMcpTransport Transport, Task Run) Start(IEnumerable<IMcpMethodHandler> handlers, CancellationToken cancellationToken = default)
    {
        FakeMcpTransport transport = new();
        Task run = new McpServer(transport, ServerInfo, handlers).RunAsync(cancellationToken);
        return (transport, run);
    }

    private static async Task<List<string>> EndAsync(FakeMcpTransport transport, Task run)
    {
        transport.End();
        await run.WaitAsync(TimeSpan.FromSeconds(10));
        return transport.RemainingResponses();
    }

    private static void AssertJsonEqual(string expected, string actual)
    {
        using JsonDocument expectedDocument = JsonDocument.Parse(expected);
        using JsonDocument actualDocument = JsonDocument.Parse(actual);
        Assert.IsTrue(JsonElement.DeepEquals(expectedDocument.RootElement, actualDocument.RootElement), $"Expected {expected}, got {actual}");
    }

    [TestMethod]
    [DataRow("2024-11-05", "2024-11-05")]
    [DataRow("2025-06-18", "2025-06-18")]
    [DataRow("2025-11-25", "2025-11-25")]
    [DataRow("1999-01-01", "2025-11-25")]
    public async Task RunAsync_InitializeGoldenConversation_AnswersAsTheSdkServerDid(string clientVersion, string answeredVersion)
    {
        (FakeMcpTransport transport, Task run) = Start(GoldenHandlers);

        transport.Send(InitializeLine.Replace("{0}", clientVersion, StringComparison.Ordinal));
        string response = await transport.NextResponseAsync();
        transport.Send("""{"jsonrpc":"2.0","method":"notifications/initialized"}""");

        AssertJsonEqual(GoldenInitializeResponse.Replace("{0}", answeredVersion, StringComparison.Ordinal), response);
        Assert.IsEmpty(await EndAsync(transport, run));
    }

    [TestMethod]
    public async Task RunAsync_PingGoldenConversation_AnswersEmptyResult()
    {
        (FakeMcpTransport transport, Task run) = Start(GoldenHandlers);

        transport.Send(InitializeLine.Replace("{0}", "2025-06-18", StringComparison.Ordinal));
        _ = await transport.NextResponseAsync();
        transport.Send("""{"jsonrpc":"2.0","method":"notifications/initialized"}""");
        transport.Send("""{"jsonrpc":"2.0","id":2,"method":"ping","params":{}}""");

        AssertJsonEqual("""{"result":{},"id":2,"jsonrpc":"2.0"}""", await transport.NextResponseAsync());
        Assert.IsEmpty(await EndAsync(transport, run));
    }

    [TestMethod]
    public async Task RunAsync_UnknownMethodGoldenConversation_AnswersMethodNotFound()
    {
        (FakeMcpTransport transport, Task run) = Start(GoldenHandlers);

        transport.Send("""{"jsonrpc":"2.0","id":2,"method":"no/such/method","params":{}}""");

        AssertJsonEqual("""{"error":{"code":-32601,"message":"Method 'no/such/method' is not available."},"id":2,"jsonrpc":"2.0"}""", await transport.NextResponseAsync());
        Assert.IsEmpty(await EndAsync(transport, run));
    }

    [TestMethod]
    [DataRow("{this is not json")]
    [DataRow("42")]
    [DataRow("""[{"jsonrpc":"2.0","id":2,"method":"ping","params":{}},{"jsonrpc":"2.0","id":3,"method":"ping","params":{}}]""")]
    public async Task RunAsync_MalformedGoldenConversation_AnswersNothing(string line)
    {
        (FakeMcpTransport transport, Task run) = Start(GoldenHandlers);

        transport.Send(line);

        Assert.IsEmpty(await EndAsync(transport, run));
    }

    [TestMethod]
    public async Task RunAsync_InvalidRequestWithId_AnswersInvalidRequest()
    {
        (FakeMcpTransport transport, Task run) = Start([]);

        transport.Send("""{"id":3,"method":"ping"}""");

        Assert.AreEqual("""{"jsonrpc":"2.0","id":3,"error":{"code":-32600,"message":"Invalid Request"}}""", await transport.NextResponseAsync());
        Assert.IsEmpty(await EndAsync(transport, run));
    }

    [TestMethod]
    public async Task RunAsync_NoHandlers_InitializeListsNoCapabilities()
    {
        (FakeMcpTransport transport, Task run) = Start([]);

        transport.Send(InitializeLine.Replace("{0}", "2025-06-18", StringComparison.Ordinal));

        AssertJsonEqual("""{"jsonrpc":"2.0","id":1,"result":{"protocolVersion":"2025-06-18","capabilities":{},"serverInfo":{"name":"Curl.Mcp","version":"0.1.0.0"}}}""", await transport.NextResponseAsync());
        Assert.IsEmpty(await EndAsync(transport, run));
    }

    [TestMethod]
    public async Task RunAsync_OnlyToolsHandler_InitializeListsOnlyTools()
    {
        (FakeMcpTransport transport, Task run) = Start([new StubCapabilityHandler("tools/list", "tools", """{"listChanged":true}"""), new McpPingHandler()]);

        transport.Send(InitializeLine.Replace("{0}", "2025-06-18", StringComparison.Ordinal));

        AssertJsonEqual("""{"jsonrpc":"2.0","id":1,"result":{"protocolVersion":"2025-06-18","capabilities":{"tools":{"listChanged":true}},"serverInfo":{"name":"Curl.Mcp","version":"0.1.0.0"}}}""", await transport.NextResponseAsync());
        Assert.IsEmpty(await EndAsync(transport, run));
    }

    [TestMethod]
    public async Task RunAsync_RequestBeforeInitialize_IsAnsweredAsTheSdkServerDid()
    {
        (FakeMcpTransport transport, Task run) = Start(GoldenHandlers);

        transport.Send("""{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}""");

        Assert.AreEqual("""{"jsonrpc":"2.0","id":1,"result":{}}""", await transport.NextResponseAsync());
        Assert.IsEmpty(await EndAsync(transport, run));
    }

    [TestMethod]
    [DataRow("""{"jsonrpc":"2.0","id":1,"method":"initialize"}""")]
    [DataRow("""{"jsonrpc":"2.0","id":1,"method":"initialize","params":[]}""")]
    [DataRow("""{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}""")]
    [DataRow("""{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":7}}""")]
    public async Task RunAsync_InitializeWithInvalidParams_AnswersInvalidParams(string line)
    {
        (FakeMcpTransport transport, Task run) = Start([]);

        transport.Send(line);

        Assert.AreEqual("""{"jsonrpc":"2.0","id":1,"error":{"code":-32602,"message":"The initialize request needs a protocolVersion string in its params."}}""", await transport.NextResponseAsync());
        Assert.IsEmpty(await EndAsync(transport, run));
    }

    [TestMethod]
    public async Task RunAsync_HandlerThrowsProtocolException_AnswersItsCodeAndMessage()
    {
        (FakeMcpTransport transport, Task run) = Start([StubMcpMethodHandler.Throwing("resources/read", new McpProtocolException(JsonRpcErrorCodes.ResourceNotFound, "Resource not found"))]);

        transport.Send("""{"jsonrpc":"2.0","id":"r","method":"resources/read"}""");

        Assert.AreEqual("""{"jsonrpc":"2.0","id":"r","error":{"code":-32002,"message":"Resource not found"}}""", await transport.NextResponseAsync());
        Assert.IsEmpty(await EndAsync(transport, run));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task RunAsync_HandlerThrowsOtherException_AnswersInternalErrorWithoutDetail(bool canceled)
    {
        Exception exception = canceled ? new OperationCanceledException("secret detail") : new InvalidOperationException("secret detail");
        (FakeMcpTransport transport, Task run) = Start([StubMcpMethodHandler.Throwing("tools/call", exception)]);

        transport.Send("""{"jsonrpc":"2.0","id":5,"method":"tools/call"}""");
        transport.Send("""{"jsonrpc":"2.0","id":6,"method":"ping"}""");
        List<string> responses = [await transport.NextResponseAsync(), await transport.NextResponseAsync()];

        CollectionAssert.Contains(responses, """{"jsonrpc":"2.0","id":5,"error":{"code":-32603,"message":"An error occurred."}}""");
        CollectionAssert.Contains(responses, """{"jsonrpc":"2.0","id":6,"result":{}}""");
        Assert.IsEmpty(await EndAsync(transport, run));
    }

    [TestMethod]
    public async Task RunAsync_BlockedHandler_DoesNotDelayPingAndIsCancelledByNotification()
    {
        TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource cancelled = new(TaskCreationOptions.RunContinuationsAsynchronously);
        (FakeMcpTransport transport, Task run) = Start([StubMcpMethodHandler.Blocking("tools/call", started, cancelled)]);

        transport.Send("""{"jsonrpc":"2.0","id":1,"method":"tools/call"}""");
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        transport.Send("""{"jsonrpc":"2.0","id":2,"method":"ping"}""");
        string pingResponse = await transport.NextResponseAsync();
        transport.Send("""{"jsonrpc":"2.0","method":"notifications/cancelled","params":{"requestId":1,"reason":"user"}}""");
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(10));
        transport.Send("""{"jsonrpc":"2.0","id":3,"method":"ping"}""");

        Assert.AreEqual("""{"jsonrpc":"2.0","id":2,"result":{}}""", pingResponse);
        Assert.AreEqual("""{"jsonrpc":"2.0","id":3,"result":{}}""", await transport.NextResponseAsync());
        Assert.IsEmpty(await EndAsync(transport, run));
    }

    [TestMethod]
    [DataRow("""{"jsonrpc":"2.0","method":"notifications/cancelled"}""")]
    [DataRow("""{"jsonrpc":"2.0","method":"notifications/cancelled","params":{}}""")]
    [DataRow("""{"jsonrpc":"2.0","method":"notifications/cancelled","params":{"requestId":{}}}""")]
    [DataRow("""{"jsonrpc":"2.0","method":"notifications/cancelled","params":{"requestId":"1"}}""")]
    [DataRow("""{"jsonrpc":"2.0","method":"notifications/cancelled","params":{"requestId":99}}""")]
    [DataRow("""{"jsonrpc":"2.0","method":"notifications/unknown"}""")]
    [DataRow("""{"jsonrpc":"2.0","method":"notifications/failing"}""")]
    public async Task RunAsync_NotificationNotNamingTheRequest_LeavesItRunning(string notification)
    {
        TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource cancelled = new(TaskCreationOptions.RunContinuationsAsynchronously);
        (FakeMcpTransport transport, Task run) = Start(
        [
            StubMcpMethodHandler.Blocking("tools/call", started, cancelled),
            StubMcpMethodHandler.Throwing("notifications/failing", new InvalidOperationException("boom")),
        ]);

        transport.Send("""{"jsonrpc":"2.0","id":1,"method":"tools/call"}""");
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        transport.Send(notification);
        transport.Send("""{"jsonrpc":"2.0","id":2,"method":"ping"}""");
        string pingResponse = await transport.NextResponseAsync();

        Assert.AreEqual("""{"jsonrpc":"2.0","id":2,"result":{}}""", pingResponse);
        Assert.IsFalse(cancelled.Task.IsCompleted);
        Assert.IsEmpty(await EndAsync(transport, run));
        Assert.IsTrue(cancelled.Task.IsCompleted);
    }

    [TestMethod]
    public async Task RunAsync_TransportEnds_CancelsRequestsInFlightWithoutAnswering()
    {
        TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource cancelled = new(TaskCreationOptions.RunContinuationsAsynchronously);
        (FakeMcpTransport transport, Task run) = Start([StubMcpMethodHandler.Blocking("tools/call", started, cancelled)]);

        transport.Send("""{"jsonrpc":"2.0","id":"a","method":"tools/call"}""");
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.IsEmpty(await EndAsync(transport, run));
        Assert.IsTrue(cancelled.Task.IsCompleted);
    }

    [TestMethod]
    public async Task RunAsync_HostCancels_EndsLoopAndCancelsRequestsInFlight()
    {
        TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource cancelled = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using CancellationTokenSource host = new();
        (FakeMcpTransport transport, Task run) = Start([StubMcpMethodHandler.Blocking("tools/call", started, cancelled)], host.Token);

        transport.Send("""{"jsonrpc":"2.0","id":1,"method":"tools/call"}""");
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await host.CancelAsync();
        await run.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.IsTrue(cancelled.Task.IsCompleted);
        Assert.IsEmpty(transport.RemainingResponses());
    }

    [TestMethod]
    public async Task RunAsync_TransportReadFails_RethrowsTheFailure()
    {
        FakeMcpTransport transport = new() { ReadFailure = new IOException("pipe broken") };

        await Assert.ThrowsExactlyAsync<IOException>(() => new McpServer(transport, ServerInfo, []).RunAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task RunAsync_TransportReadCancelledWithoutHostCancelling_EndsLoop()
    {
        FakeMcpTransport transport = new() { ReadFailure = new OperationCanceledException() };

        await new McpServer(transport, ServerInfo, []).RunAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.IsEmpty(transport.RemainingResponses());
    }

    [TestMethod]
    public async Task RunAsync_HostCancelsWhileReadIgnoresItsToken_CompletesPromptly()
    {
        using CancellationTokenSource host = new();
        TokenIgnoringTransport transport = new();
        Task run = new McpServer(transport, ServerInfo, []).RunAsync(host.Token);

        await transport.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await host.CancelAsync();
        await run.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.IsFalse(transport.ReadFinished.Task.IsCompleted);
    }

    // A transport whose read blocks and never looks at its token, as the console's standard input does.
    private sealed class TokenIgnoringTransport : IMcpTransport
    {
        public TaskCompletionSource ReadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ReadFinished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask<JsonRpcIncoming?> ReadMessageAsync(CancellationToken cancellationToken)
        {
            ReadStarted.TrySetResult();
            await ReadFinished.Task;
            return null;
        }

        public ValueTask WriteMessageAsync(ReadOnlyMemory<byte> message, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }}
