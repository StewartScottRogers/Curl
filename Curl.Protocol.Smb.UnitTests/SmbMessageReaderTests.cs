using Curl.Protocol.Smb.Fakes;
using Curl.Testing;

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
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ReceiveAsync_MessageSplitAcrossReads_ReturnsItWhole()
    {
        byte[] message = SmbRecordedExchange.SessionSetupAccepted;
        var connection = new ScriptedConnection(message[..2], message[2..10], message[10..]);

        SmbReceivedMessage received = await Reader(connection).ReceiveAsync(CancellationToken.None);

        ActReceived("received", received);
        Diagnostics.Diff("received", message, received.Bytes ?? []);
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

        ActReceived("received", received);
        ActReceived("next", next);
        Diagnostics.Assert("received length", first.Length + 2, received.Bytes?.Length);
        Diagnostics.Diff("next", second, next.Bytes ?? []);
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

        Diagnostics.Arrange("netbios header", $"{netBiosHeader}, frame {frameSize} bytes");

        SmbReceivedMessage received = await Reader(new ScriptedConnection(message)).ReceiveAsync(CancellationToken.None);

        ActReceived("received", received);
        Diagnostics.Diff("received", message, received.Bytes ?? []);
        CollectionAssert.AreEqual(message, received.Bytes);
    }

    [TestMethod]
    [DataRow("00 00 8f fd", "too large NetBIOS frame size 36865")]
    [DataRow("00 00 00 1f", "too small NetBIOS frame size 35")]
    public async Task ReceiveAsync_FrameSizeOutOfRange_FailsWithCurlsText(string netBiosHeader, string expected)
    {
        var connection = new ScriptedConnection(SmbRecordedExchange.Hex(netBiosHeader));

        SmbReceivedMessage received = await Reader(connection).ReceiveAsync(CancellationToken.None);

        ActReceived("received", received);
        Diagnostics.Diff("error message", expected, received.ErrorMessage ?? string.Empty);
        Assert.IsNull(received.Bytes);
        Assert.AreEqual(expected, received.ErrorMessage);
    }

    [TestMethod]
    public async Task ReceiveAsync_ByteCountPastTheFrame_FailsAsAReceiveError()
    {
        byte[] message = SmbRecordedExchange.SessionSetupRefused;
        Diagnostics.Arrange("byte count", "set to 1 past the frame");
        message[37] = 0x01;

        SmbReceivedMessage received = await Reader(new ScriptedConnection(message)).ReceiveAsync(CancellationToken.None);

        ActReceived("received", received);
        Diagnostics.Diff("error message", "Failure when receiving data from the peer", received.ErrorMessage ?? string.Empty);
        Assert.AreEqual("Failure when receiving data from the peer", received.ErrorMessage);
    }

    [TestMethod]
    public async Task ReceiveAsync_ServerClosesBeforeTheMessageIsWhole_WaitsUntilCancelled()
    {
        var connection = new ScriptedConnection(SmbRecordedExchange.SessionSetupAccepted[..3]);
        using var cancellation = new CancellationTokenSource();
        Diagnostics.Arrange("cancellation token", "cancelled; the server sends 3 bytes and closes");
        await cancellation.CancelAsync();

        var thrown = await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await Reader(connection).ReceiveAsync(cancellation.Token));

        Diagnostics.Act("thrown", thrown.GetType().Name);
        Diagnostics.Assert("thrown", nameof(OperationCanceledException) + " or a subclass", thrown.GetType().Name);
    }

    private SmbMessageReader Reader(ScriptedConnection connection)
    {
        Diagnostics.ArrangeReplies(connection);
        return new(connection, TimeProvider.System);
    }

    private void ActReceived(string label, SmbReceivedMessage received)
    {
        Diagnostics.Act(label, received.Bytes is { } bytes ? $"{bytes.Length} bytes: {SmbDiagnostics.Messages(bytes)}" : $"error \"{received.ErrorMessage}\"");
        if (received.Bytes is { } message)
        {
            Diagnostics.Bytes(label, message);
        }
    }
}
