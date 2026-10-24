using System.Text;
using System.Text.Json;
using Curl.Mcp.Protocol.JsonRpc;

namespace Curl.Mcp.Protocol.Transport;

[TestClass]
public sealed class StdioMcpTransportTests
{
    private const string Notification = """{"jsonrpc":"2.0","method":"m"}""";

    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);

    private static StdioMcpTransport Reading(params string[] chunks) =>
        new(new ChunkedInputStream(chunks.Select(Utf8), waitAfterLastChunk: false), new MemoryStream());

    private static async Task<List<JsonRpcIncoming>> ReadAllAsync(StdioMcpTransport transport)
    {
        List<JsonRpcIncoming> messages = [];
        while (await transport.ReadMessageAsync(CancellationToken.None) is { } message)
        {
            messages.Add(message);
        }

        return messages;
    }

    private static string MethodOf(JsonRpcIncoming message) => ((JsonRpcNotification)message).Method;

    [TestMethod]
    public async Task ReadMessageAsync_ManyMessagesInOneRead_ReturnsEachInOrder()
    {
        using StdioMcpTransport transport = Reading(
            """{"jsonrpc":"2.0","method":"a"}""" + "\n" + """{"jsonrpc":"2.0","id":7,"method":"b"}""" + "\n" + """{"jsonrpc":"2.0","method":"c"}""" + "\n");

        List<JsonRpcIncoming> messages = await ReadAllAsync(transport);

        Assert.HasCount(3, messages);
        Assert.AreEqual("a", MethodOf(messages[0]));
        Assert.AreEqual("b", ((JsonRpcRequest)messages[1]).Method);
        Assert.AreEqual("c", MethodOf(messages[2]));
    }

    [TestMethod]
    public async Task ReadMessageAsync_ReadSplitInsideMultibyteCharacter_JoinsTheCharacter()
    {
        byte[] line = Utf8("""{"jsonrpc":"2.0","method":"café€"}""" + "\n");
        int split = Array.IndexOf(line, (byte)0xE2) + 1;
        using var transport = new StdioMcpTransport(
            new ChunkedInputStream([line[..split], line[split..]], waitAfterLastChunk: false),
            new MemoryStream());

        List<JsonRpcIncoming> messages = await ReadAllAsync(transport);

        Assert.HasCount(1, messages);
        Assert.AreEqual("café€", MethodOf(messages[0]));
    }

    [TestMethod]
    public async Task ReadMessageAsync_CarriageReturnLineFeed_ReadsTheMessage()
    {
        using StdioMcpTransport transport = Reading(Notification + "\r\n" + Notification + "\r\n");

        List<JsonRpcIncoming> messages = await ReadAllAsync(transport);

        Assert.HasCount(2, messages);
        Assert.AreEqual("m", MethodOf(messages[1]));
    }

    [TestMethod]
    public async Task ReadMessageAsync_BlankAndWhitespaceLines_SkipsThem()
    {
        using StdioMcpTransport transport = Reading("\n\r\n \t \n", Notification + "\n\n");

        List<JsonRpcIncoming> messages = await ReadAllAsync(transport);

        Assert.HasCount(1, messages);
        Assert.AreEqual("m", MethodOf(messages[0]));
    }

    [TestMethod]
    public async Task ReadMessageAsync_FinalLineUnterminated_DeliversItThenNull()
    {
        using StdioMcpTransport transport = Reading(Notification + "\n" + """{"jsonrpc":""", "\"2.0\",\"method\":\"last\"}");

        List<JsonRpcIncoming> messages = await ReadAllAsync(transport);

        Assert.HasCount(2, messages);
        Assert.AreEqual("last", MethodOf(messages[1]));
    }

    [TestMethod]
    public async Task ReadMessageAsync_EndOfInput_ReturnsNullEveryTime()
    {
        using StdioMcpTransport transport = Reading();

        JsonRpcIncoming? first = await transport.ReadMessageAsync(CancellationToken.None);
        JsonRpcIncoming? second = await transport.ReadMessageAsync(CancellationToken.None);

        Assert.IsNull(first);
        Assert.IsNull(second);
    }

    [TestMethod]
    public async Task ReadMessageAsync_LineIsNotJson_ReturnsParseError()
    {
        using StdioMcpTransport transport = Reading("not json\n");

        JsonRpcIncoming? message = await transport.ReadMessageAsync(CancellationToken.None);

        Assert.AreEqual(JsonRpcErrorCodes.ParseError, ((JsonRpcInvalidMessage)message!).Error.Code);
    }

    [TestMethod]
    public async Task ReadMessageAsync_LineOneByteUnderTheLimit_ReadsTheMessage()
    {
        string line = Notification.PadRight(StdioMcpTransport.MaximumLineLength - 1);
        using var transport = new StdioMcpTransport(new MemoryStream(Utf8(line + "\n")), new MemoryStream());

        JsonRpcIncoming? message = await transport.ReadMessageAsync(CancellationToken.None);

        Assert.AreEqual("m", MethodOf(message!));
    }

    [TestMethod]
    public async Task ReadMessageAsync_LineOverTheLimit_ReturnsInvalidRequestThenReadsTheNextLine()
    {
        string line = Notification.PadRight(StdioMcpTransport.MaximumLineLength);
        using var transport = new StdioMcpTransport(
            new MemoryStream(Utf8(line + "\n" + """{"jsonrpc":"2.0","method":"next"}""" + "\n")),
            new MemoryStream());

        List<JsonRpcIncoming> messages = await ReadAllAsync(transport);

        Assert.HasCount(2, messages);
        var invalid = (JsonRpcInvalidMessage)messages[0];
        Assert.AreEqual(JsonRpcErrorCodes.InvalidRequest, invalid.Error.Code);
        Assert.IsNull(invalid.Id);
        Assert.AreEqual("next", MethodOf(messages[1]));
    }

    [TestMethod]
    public async Task ReadMessageAsync_UnterminatedLineOverTheLimit_ReturnsInvalidRequestThenNull()
    {
        using var transport = new StdioMcpTransport(
            new MemoryStream(Utf8(Notification.PadRight(StdioMcpTransport.MaximumLineLength + 10))),
            new MemoryStream());

        List<JsonRpcIncoming> messages = await ReadAllAsync(transport);

        Assert.HasCount(1, messages);
        Assert.AreEqual(JsonRpcErrorCodes.InvalidRequest, ((JsonRpcInvalidMessage)messages[0]).Error.Code);
    }

    [TestMethod]
    public async Task ReadMessageAsync_CancelledWhileWaitingForInput_ThrowsOperationCanceled()
    {
        var input = new ChunkedInputStream([], waitAfterLastChunk: true);
        using var transport = new StdioMcpTransport(input, new MemoryStream());
        using var cancellation = new CancellationTokenSource();

        ValueTask<JsonRpcIncoming?> pending = transport.ReadMessageAsync(cancellation.Token);
        Assert.IsFalse(pending.IsCompleted);
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await pending);
        Assert.AreEqual(1, input.ReadCount);
    }

    [TestMethod]
    public async Task WriteMessageAsync_Message_WritesItAndANewlineWithNoByteOrderMark()
    {
        var output = new MemoryStream();
        using var transport = new StdioMcpTransport(new MemoryStream(), output);

        await transport.WriteMessageAsync(Utf8("""{"jsonrpc":"2.0","id":1,"result":{}}"""), CancellationToken.None);

        Assert.AreEqual("""{"jsonrpc":"2.0","id":1,"result":{}}""" + "\n", Encoding.UTF8.GetString(output.ToArray()));
        Assert.AreEqual((byte)'{', output.ToArray()[0]);
    }

    [TestMethod]
    public async Task WriteMessageAsync_BufferedOutput_FlushesEachMessage()
    {
        var underlying = new MemoryStream();
        await using var buffered = new BufferedStream(underlying, 4096);
        using var transport = new StdioMcpTransport(new MemoryStream(), buffered);

        await transport.WriteMessageAsync(Utf8(Notification), CancellationToken.None);

        Assert.AreEqual(Notification + "\n", Encoding.UTF8.GetString(underlying.ToArray()));
    }

    [TestMethod]
    public async Task WriteMessageAsync_MessageContainsNewline_ThrowsArgumentException()
    {
        var output = new MemoryStream();
        using var transport = new StdioMcpTransport(new MemoryStream(), output);

        await Assert.ThrowsExactlyAsync<ArgumentException>(
            async () => await transport.WriteMessageAsync(Utf8("{}\n{}"), CancellationToken.None));
        Assert.AreEqual(0, output.Length);
    }

    [TestMethod]
    public async Task WriteMessageAsync_TwoTasksWriteAThousandResponsesEach_WritesTwoThousandWholeLines()
    {
        var output = new YieldingOutputStream();
        using var transport = new StdioMcpTransport(new MemoryStream(), output);
        var writer = new JsonRpcResponseWriter();

        async Task WriteResponsesAsync(string prefix)
        {
            for (int index = 0; index < 1000; index++)
            {
                JsonRpcRequestId id = ((JsonRpcRequest)new JsonRpcMessageParser().Parse(
                    Utf8($$"""{"jsonrpc":"2.0","id":"{{prefix}}{{index}}","method":"m"}"""))).Id;
                using JsonDocument result = JsonDocument.Parse($$"""{"text":"{{new string('x', index % 50)}}"}""");
                await transport.WriteMessageAsync(writer.WriteSuccess(id, result.RootElement), CancellationToken.None);
            }
        }

        await Task.WhenAll(Task.Run(() => WriteResponsesAsync("a")), Task.Run(() => WriteResponsesAsync("b")));

        string[] lines = Encoding.UTF8.GetString(output.ToArray()).Split('\n');
        Assert.HasCount(2001, lines);
        Assert.AreEqual(string.Empty, lines[^1]);
        HashSet<string> ids = [];
        foreach (string line in lines[..^1])
        {
            using JsonDocument response = JsonDocument.Parse(line);
            Assert.IsTrue(ids.Add(response.RootElement.GetProperty("id").GetString()!));
        }

        Assert.HasCount(2000, ids);
    }

    [TestMethod]
    public async Task WriteMessageAsync_CancelledBeforeWriting_ThrowsOperationCanceled()
    {
        var output = new MemoryStream();
        using var transport = new StdioMcpTransport(new MemoryStream(), output);

        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await transport.WriteMessageAsync(Utf8(Notification), new CancellationToken(canceled: true)));
        Assert.AreEqual(0, output.Length);
    }

    [TestMethod]
    public async Task Dispose_ThenWriteMessageAsync_ThrowsObjectDisposed()
    {
        var transport = new StdioMcpTransport(new MemoryStream(), new MemoryStream());

        transport.Dispose();

        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(
            async () => await transport.WriteMessageAsync(Utf8(Notification), CancellationToken.None));
    }

    [TestMethod]
    public void Constructor_NullInput_ThrowsArgumentNull()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new StdioMcpTransport(null!, new MemoryStream()));
    }

    [TestMethod]
    public void Constructor_NullOutput_ThrowsArgumentNull()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new StdioMcpTransport(new MemoryStream(), null!));
    }

    // Yields before each write so a writer that held no lock would let the other write between its two parts.
    private sealed class YieldingOutputStream : MemoryStream
    {
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            await base.WriteAsync(buffer, cancellationToken);
        }
    }
}
