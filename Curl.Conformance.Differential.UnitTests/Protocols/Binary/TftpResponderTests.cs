using System.Text;
using Curl.Conformance.Differential.Protocols.Binary;
using Curl.Conformance.Differential.Protocols.Core;

namespace Curl.Conformance.Differential.UnitTests.Protocols.Binary;

[TestClass]
public sealed class TftpResponderTests
{
    private static readonly byte[] FileDatagram = [0, 3, 0, 1, .. "tftp file contents\n"u8];

    [TestMethod]
    public async Task RespondAsync_ReadRequest_SendsTheBlockAndLogsTheAck()
    {
        var socket = new FakeTransferSocket(new byte[] { 0, 4, 0, 1 });

        var log = await RunAsync([0, 1, .. "f.txt\0octet\0"u8], socket);

        Assert.AreEqual("op=1 file=b'f.txt' mode=b'octet' opts=[b'']\nack=00040001", log);
        CollectionAssert.AreEqual(FileDatagram, socket.Sent.Single());
    }

    [TestMethod]
    public async Task RespondAsync_ReadRequestWithNoAck_LogsOnlyTheRequest()
    {
        var log = await RunAsync([0, 1, .. "f\0m\0blksize\0512\0"u8], new FakeTransferSocket());

        Assert.AreEqual("op=1 file=b'f' mode=b'm' opts=[b'blksize', b'512', b'']", log);
    }

    [TestMethod]
    public async Task RespondAsync_LongAck_LogsItsFirstHundredBytes()
    {
        var log = await RunAsync([0, 1, .. "f\0m\0"u8], new FakeTransferSocket(new byte[150]));

        Assert.AreEqual("op=1 file=b'f' mode=b'm' opts=[b'']\nack=" + new string('0', 200), log);
    }

    [TestMethod]
    public async Task RespondAsync_WriteRequest_AcknowledgesEachBlockUntilAShortOneAndLogsWhatWasStored()
    {
        var full = new byte[] { 0, 3, 0, 1 }.Concat(Enumerable.Repeat((byte)'a', 512)).ToArray();
        var socket = new FakeTransferSocket(full, new byte[] { 0, 3, 0, 2, (byte)'b', (byte)'c' });

        var log = await RunAsync([0, 2, .. "up\0octet\0"u8], socket);

        Assert.AreEqual("op=2 file=b'up' mode=b'octet' opts=[b'']\nstored=b'" + new string('a', 512) + "bc'", log);
        Assert.HasCount(3, socket.Sent);
        CollectionAssert.AreEqual(new byte[] { 0, 4, 0, 0 }, socket.Sent[0]);
        CollectionAssert.AreEqual(new byte[] { 0, 4, 0, 1 }, socket.Sent[1]);
        CollectionAssert.AreEqual(new byte[] { 0, 4, 0, 2 }, socket.Sent[2]);
    }

    [TestMethod]
    public async Task RespondAsync_OversizedBlock_StoresOnlyTheFirstThousandBytes()
    {
        var socket = new FakeTransferSocket(new byte[] { 0, 3, 0, 1 }.Concat(Enumerable.Repeat((byte)'z', 1096)).ToArray());

        var log = await RunAsync([0, 2, .. "up\0m\0"u8], socket);

        Assert.AreEqual("op=2 file=b'up' mode=b'm' opts=[b'']\nstored=b'" + new string('z', 996) + "'", log);
    }

    [TestMethod]
    public async Task RespondAsync_WriteRequestThenNothing_LogsEmptyStore()
    {
        var socket = new FakeTransferSocket();

        var log = await RunAsync([0, 2, .. "up\0m\0"u8], socket);

        Assert.AreEqual("op=2 file=b'up' mode=b'm' opts=[b'']\nstored=b''", log);
        Assert.HasCount(1, socket.Sent);
    }

    [TestMethod]
    public async Task RespondAsync_WriteRequestThenTooShortADatagram_LogsEmptyStore()
    {
        var socket = new FakeTransferSocket(new byte[] { 0, 3, 0 });

        var log = await RunAsync([0, 2, .. "up\0m\0"u8], socket);

        Assert.AreEqual("op=2 file=b'up' mode=b'm' opts=[b'']\nstored=b''", log);
        Assert.HasCount(1, socket.Sent);
    }

    [TestMethod]
    public async Task RespondAsync_OtherOpcode_LogsAndSendsNothing()
    {
        var socket = new FakeTransferSocket();

        var log = await RunAsync([0, 5, .. "f\0m\0"u8], socket);

        Assert.AreEqual("op=5 file=b'f' mode=b'm' opts=[b'']", log);
        Assert.IsEmpty(socket.Sent);
    }

    [TestMethod]
    [DataRow(new byte[0])]
    [DataRow(new byte[] { 0 })]
    [DataRow(new byte[] { 0, 1 })]
    [DataRow(new byte[] { 0, 1, 0x66 })]
    public async Task RespondAsync_RequestWithoutAFileAndAMode_IsIgnored(byte[] request)
    {
        var socket = new FakeTransferSocket();

        var log = await RunAsync(request, socket);

        Assert.AreEqual(string.Empty, log);
        Assert.IsEmpty(socket.Sent);
    }

    private static async Task<string> RunAsync(byte[] request, FakeTransferSocket socket)
    {
        var log = new ProtocolWireLog();

        await new TftpResponder(log).RespondAsync(request, socket, CancellationToken.None);

        return Encoding.UTF8.GetString(log.Take());
    }

    private sealed class FakeTransferSocket(params byte[][] replies) : ITftpTransferSocket
    {
        private readonly Queue<byte[]> _replies = new(replies);

        public List<byte[]> Sent { get; } = [];

        public Task SendAsync(byte[] datagram, CancellationToken cancellationToken)
        {
            Sent.Add(datagram);
            return Task.CompletedTask;
        }

        public Task<byte[]?> ReceiveAsync(CancellationToken cancellationToken) =>
            Task.FromResult(_replies.Count > 0 ? _replies.Dequeue() : null);
    }
}
