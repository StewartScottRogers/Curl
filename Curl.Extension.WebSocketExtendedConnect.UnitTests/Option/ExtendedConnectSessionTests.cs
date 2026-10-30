using Curl.Protocol.Abstractions;
using Curl.WebSocket;

namespace Curl.Extension.WebSocketExtendedConnect.Option;

[TestClass]
public sealed class ExtendedConnectSessionTests
{
    [TestMethod]
    public async Task RunAsync_DataPingPongClose_SendsTheDataWritesTheMessagesAnswersThePingAndEchoesTheClose()
    {
        RecordingChannel channel = new(
            [Run(WsOpcode.Text, "hel"), Run(WsOpcode.Continuation, "lo")],
            [Run(WsOpcode.Ping, "p"), Run(WsOpcode.Pong, "q"), Run(WsOpcode.Binary, "!"), Run(WsOpcode.Close, "\u0003è"), Run(WsOpcode.Text, "never")]);
        MemoryStream output = new();

        TransferResult result = await ExtendedConnectSession.RunAsync(channel, new TransferContext { Url = CurlUrl.Parse("ws://example.com/"), Output = output, PostData = "hi"u8.ToArray() });

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(6, result.BytesTransferred);
        Assert.AreEqual("hello!", System.Text.Encoding.Latin1.GetString(output.ToArray()));
        CollectionAssert.AreEqual(new[] { "Text:hi", "Pong:p", "Close:\u0003è" }, channel.Sent);
        Assert.IsTrue(channel.IsClosed);
    }

    [TestMethod]
    public async Task RunAsync_ServerEndsTheStreamWithoutClose_SucceedsWithoutSendingAnything()
    {
        RecordingChannel channel = new([Run(WsOpcode.Text, "x")]);

        TransferResult result = await ExtendedConnectSession.RunAsync(channel, new TransferContext { Url = CurlUrl.Parse("ws://example.com/"), Output = new MemoryStream() });

        Assert.AreEqual(1, result.BytesTransferred);
        Assert.IsEmpty(channel.Sent);
        Assert.IsFalse(channel.IsClosed);
    }

    private static WsFrameRun Run(WsOpcode opcode, string payload) => new(opcode, true, 0, 0, System.Text.Encoding.Latin1.GetBytes(payload));

    private sealed class RecordingChannel(params IReadOnlyList<WsFrameRun>[] reads) : IExtendedConnectChannel
    {
        private readonly Queue<IReadOnlyList<WsFrameRun>> pending = new(reads);

        public List<string> Sent { get; } = [];

        public bool IsClosed { get; private set; }

        public Task SendAsync(WsOpcode opcode, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
        {
            Sent.Add($"{opcode}:{System.Text.Encoding.Latin1.GetString(payload.Span)}");
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<WsFrameRun>?> ReceiveAsync(CancellationToken cancellationToken) =>
            Task.FromResult(pending.TryDequeue(out IReadOnlyList<WsFrameRun>? runs) ? runs : null);

        public Task CloseAsync(CancellationToken cancellationToken)
        {
            IsClosed = true;
            return Task.CompletedTask;
        }
    }
}
