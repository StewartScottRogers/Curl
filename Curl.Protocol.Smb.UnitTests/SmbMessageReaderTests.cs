using Curl.Protocol.Smb.Fakes;

namespace Curl.Protocol.Smb;

/// <summary>
/// Pins <see cref="SmbMessageReader" /> to curl 8.21.0's <c>smb_recv_message</c>: a
/// message is whole once the NetBIOS length is reached, a frame over 0x9000 or under 36
/// bytes is exit 56 with curl's text, a byte count past the frame is exit 56, and a
/// closed connection is waited on until the transfer is cancelled.
/// </summary>
[TestClass]
public sealed class SmbMessageReaderTests
{
    [TestMethod]
    public async Task ReceiveAsync_MessageSplitAcrossReads_ReturnsItWhole()
    {
        byte[] message = SmbRecordedExchange.SessionSetupAccepted;
        var connection = new ScriptedConnection(message[..2], message[2..10], message[10..]);

        SmbReceivedMessage received = await Reader(connection).ReceiveAsync(CancellationToken.None);

        CollectionAssert.AreEqual(message, received.Bytes);
        Assert.IsNull(received.ErrorMessage);
    }

    [TestMethod]
    public async Task ReceiveAsync_BytesBeyondTheFrame_AreCountedWithTheMessageAndDroppedAfter()
    {
        byte[] first = SmbRecordedExchange.SessionSetupRefused;
        byte[] second = SmbRecordedExchange.SessionSetupAccepted;
        var connection = new ScriptedConnection([.. first, 0x00, 0x00], second);
        SmbMessageReader reader = Reader(connection);

        SmbReceivedMessage received = await reader.ReceiveAsync(CancellationToken.None);
        SmbReceivedMessage next = await reader.ReceiveAsync(CancellationToken.None);

        Assert.HasCount(first.Length + 2, received.Bytes!);
        CollectionAssert.AreEqual(second, next.Bytes);
    }

    [TestMethod]
    [DataRow("00 00 00 20", 36)]
    [DataRow("00 00 00 21", 37)]
    public async Task ReceiveAsync_FrameWithoutWordOrByteCount_IsAccepted(string netBiosHeader, int frameSize)
    {
        byte[] message = new byte[frameSize];
        SmbRecordedExchange.Hex(netBiosHeader).CopyTo(message, 0);

        SmbReceivedMessage received = await Reader(new ScriptedConnection(message)).ReceiveAsync(CancellationToken.None);

        CollectionAssert.AreEqual(message, received.Bytes);
    }

    [TestMethod]
    [DataRow("00 00 8f fd", "too large NetBIOS frame size 36865")]
    [DataRow("00 00 00 1f", "too small NetBIOS frame size 35")]
    public async Task ReceiveAsync_FrameSizeOutOfRange_FailsWithCurlsText(string netBiosHeader, string expected)
    {
        var connection = new ScriptedConnection(SmbRecordedExchange.Hex(netBiosHeader));

        SmbReceivedMessage received = await Reader(connection).ReceiveAsync(CancellationToken.None);

        Assert.IsNull(received.Bytes);
        Assert.AreEqual(expected, received.ErrorMessage);
    }

    [TestMethod]
    public async Task ReceiveAsync_ByteCountPastTheFrame_FailsAsAReceiveError()
    {
        byte[] message = SmbRecordedExchange.SessionSetupRefused;
        message[37] = 0x01;

        SmbReceivedMessage received = await Reader(new ScriptedConnection(message)).ReceiveAsync(CancellationToken.None);

        Assert.AreEqual("Failure when receiving data from the peer", received.ErrorMessage);
    }

    [TestMethod]
    public async Task ReceiveAsync_ServerClosesBeforeTheMessageIsWhole_WaitsUntilCancelled()
    {
        var connection = new ScriptedConnection(SmbRecordedExchange.SessionSetupAccepted[..3]);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await Reader(connection).ReceiveAsync(cancellation.Token));
    }

    private static SmbMessageReader Reader(ScriptedConnection connection) => new(connection, TimeProvider.System);
}
