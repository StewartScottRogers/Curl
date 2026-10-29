using System.Text;
using Curl.Extension.McpClient.Wire;

namespace Curl.Extension.McpClient.Stdio;

/// <summary>
/// Each test drives a session over in-memory standard streams against one rule of the Model Context
/// Protocol 2025-06-18, and names the specification section that sets it.
/// </summary>
[TestClass]
public sealed class McpStdioSessionTests
{
    [TestMethod]
    public async Task SendRequestAsync_Request_IsWrittenAsOneLineOnStandardInput()
    {
        // "Transports > stdio": the client writes JSON-RPC messages to stdin, delimited by newlines.
        var process = new InMemoryMcpServerProcess("""{"jsonrpc":"2.0","id":1,"result":{}}""" + "\n");
        await using var session = new McpStdioSession(process, TimeProvider.System);
        var request = new McpRequestWriter().WriteListTools(cursor: null);

        await session.SendRequestAsync(request, CancellationToken.None);

        Assert.AreEqual(Encoding.UTF8.GetString(request.Bytes) + "\n", process.WrittenInput);
    }

    [TestMethod]
    public async Task SendRequestAsync_NotificationsAndOtherResponsesFirst_ReturnsTheAwaitedResponse()
    {
        // "Transports > stdio": the server writes messages to stdout; "Overview": a notification has
        // no id and a response carries its request's id, so only the awaited id answers.
        var process = new InMemoryMcpServerProcess(
            """{"jsonrpc":"2.0","method":"notifications/message","params":{}}""" + "\n"
            + "\n"
            + """{"jsonrpc":"2.0","id":1,"method":"ping"}""" + "\n"
            + """{"jsonrpc":"2.0","id":9,"result":{}}""" + "\n"
            + """{"jsonrpc":"2.0","id":1,"result":{"tools":[]}}""" + "\r\n");
        await using var session = new McpStdioSession(process, TimeProvider.System);

        var response = await session.SendRequestAsync(new McpRequestWriter().WriteListTools(cursor: null), CancellationToken.None);

        Assert.AreEqual(1L, response.Id);
        Assert.AreEqual("""{"tools":[]}""", response.Result!.Value.GetRawText());
    }

    [TestMethod]
    public async Task SendRequestAsync_ServerExitsBeforeAnswering_ThrowsMcpServerExitedException()
    {
        // "Transports > stdio": stdout ending means the server process has gone.
        var process = new InMemoryMcpServerProcess("""{"jsonrpc":"2.0","method":"notifications/message"}""" + "\n");
        await using var session = new McpStdioSession(process, TimeProvider.System);

        var exception = await Assert.ThrowsExactlyAsync<McpServerExitedException>(
            () => session.SendRequestAsync(new McpRequestWriter().WriteListTools(cursor: null), CancellationToken.None));

        Assert.AreEqual("The MCP server exited before it answered request 1.", exception.Message);
    }

    [TestMethod]
    public async Task SendRequestAsync_LineThatIsNotJsonRpc_ThrowsMcpWireFormatException()
    {
        // "Transports > stdio": the server must not write anything to stdout that is not a valid MCP message.
        var process = new InMemoryMcpServerProcess("server starting\n");
        await using var session = new McpStdioSession(process, TimeProvider.System);

        await Assert.ThrowsExactlyAsync<McpWireFormatException>(
            () => session.SendRequestAsync(new McpRequestWriter().WriteListTools(cursor: null), CancellationToken.None));
    }

    [TestMethod]
    public async Task SendMessageAsync_Notification_IsWrittenAsOneLine()
    {
        // "Lifecycle > Initialization": the client sends notifications/initialized; "Transports > stdio": one line each.
        var process = new InMemoryMcpServerProcess(string.Empty);
        await using var session = new McpStdioSession(process, TimeProvider.System);

        await session.SendMessageAsync(McpRequestWriter.WriteInitializedNotification(), CancellationToken.None);

        Assert.AreEqual("""{"jsonrpc":"2.0","method":"notifications/initialized"}""" + "\n", process.WrittenInput);
    }

    [TestMethod]
    [DataRow("{\n}", DisplayName = "newline")]
    [DataRow("{\r}", DisplayName = "carriage return")]
    public async Task SendMessageAsync_MessageWithAnEmbeddedLineBreak_Throws(string message)
    {
        // "Transports > stdio": messages must not contain embedded newlines.
        var process = new InMemoryMcpServerProcess(string.Empty);
        await using var session = new McpStdioSession(process, TimeProvider.System);

        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => session.SendMessageAsync(Encoding.UTF8.GetBytes(message), CancellationToken.None));
        Assert.AreEqual(string.Empty, process.WrittenInput);
    }

    [TestMethod]
    public async Task EndAsync_ServerExitsWhenInputCloses_ClosesStandardInputFirstAndDoesNotKill()
    {
        // "Lifecycle > Shutdown > stdio": the client closes the input stream to the server and waits for it to exit.
        var process = new InMemoryMcpServerProcess(string.Empty);
        var session = new McpStdioSession(process, TimeProvider.System);

        await session.EndAsync();

        Assert.IsTrue(process.InputClosedBeforeExitWait);
        Assert.IsFalse(process.Killed);
        Assert.IsTrue(process.Disposed);
    }

    [TestMethod]
    public async Task EndAsync_ServerDoesNotExitInTheGracePeriod_KillsIt()
    {
        // "Lifecycle > Shutdown > stdio": a server that does not exit in a reasonable time is ended.
        var process = new InMemoryMcpServerProcess(string.Empty, exitsWhenInputCloses: false);
        var timeProvider = new FiredOnDemandTimeProvider();
        var session = new McpStdioSession(process, timeProvider);

        var ending = session.EndAsync();
        Assert.IsFalse(process.Killed);
        timeProvider.FireTimers();
        await ending;

        Assert.IsTrue(process.InputClosed);
        Assert.IsTrue(process.Killed);
        Assert.IsTrue(process.Disposed);
        CollectionAssert.AreEqual(new[] { McpStdioSession.ExitGracePeriod }, timeProvider.DueTimes);
    }

    [TestMethod]
    public async Task DisposeAsync_AfterEndAsync_DoesNothingMore()
    {
        var process = new InMemoryMcpServerProcess(string.Empty, exitsWhenInputCloses: false);
        var timeProvider = new FiredOnDemandTimeProvider();
        var session = new McpStdioSession(process, timeProvider);
        var ending = session.EndAsync();
        timeProvider.FireTimers();
        await ending;

        await session.DisposeAsync();

        Assert.HasCount(1, timeProvider.DueTimes);
    }

    [TestMethod]
    public void Constructor_NullArguments_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new McpStdioSession(null!, TimeProvider.System));
        Assert.ThrowsExactly<ArgumentNullException>(() => new McpStdioSession(new InMemoryMcpServerProcess(string.Empty), null!));
    }

    [TestMethod]
    public async Task SendMethods_NullArguments_Throw()
    {
        await using var session = new McpStdioSession(new InMemoryMcpServerProcess(string.Empty), TimeProvider.System);

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => session.SendMessageAsync(null!, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => session.SendRequestAsync(null!, CancellationToken.None));
    }
}
