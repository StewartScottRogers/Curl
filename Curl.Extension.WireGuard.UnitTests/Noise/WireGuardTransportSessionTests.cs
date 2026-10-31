using Curl.Extension.WireGuard.Messages;
using Curl.Extension.WireGuard.Noise;

namespace Curl.Extension.WireGuard.UnitTests.Noise;

[TestClass]
public sealed class WireGuardTransportSessionTests
{
    private static readonly byte[] KeyA = [.. Enumerable.Repeat((byte)0xA1, 32)];

    private static readonly byte[] KeyB = [.. Enumerable.Repeat((byte)0xB2, 32)];

    [TestMethod]
    [DataRow(0, 0)]
    [DataRow(1, 16)]
    [DataRow(16, 16)]
    [DataRow(17, 32)]
    public void Seal_PacketOfLength_PadsToAMultipleOfSixteen(int length, int paddedLength)
    {
        var (sender, receiver, _) = CreatePair();
        var packet = Enumerable.Repeat((byte)0x55, length).ToArray();

        var datagram = sender.Seal(packet);

        Assert.HasCount(TransportDataMessage.HeaderSize + paddedLength + 16, datagram);
        Assert.IsTrue(receiver.TryOpen(datagram, out var opened));
        CollectionAssert.AreEqual(packet.Concat(new byte[paddedLength - length]).ToArray(), opened);
    }

    [TestMethod]
    public void Seal_EachMessage_UsesTheNextCounterAndTheRemoteIndex()
    {
        var (sender, _, _) = CreatePair();

        TransportDataMessage.TryDecode(sender.Seal([]), out var first);
        TransportDataMessage.TryDecode(sender.Seal([]), out var second);

        Assert.AreEqual(0UL, first!.Counter);
        Assert.AreEqual(1UL, second!.Counter);
        Assert.AreEqual(sender.RemoteIndex, second.ReceiverIndex);
    }

    [TestMethod]
    public void TryOpen_ReplayedCounter_IsFalse()
    {
        var (sender, receiver, _) = CreatePair();
        var datagram = sender.Seal([1]);
        Assert.IsTrue(receiver.TryOpen(datagram, out _));

        Assert.IsFalse(receiver.TryOpen(datagram, out var packet));
        Assert.IsNull(packet);
    }

    [TestMethod]
    public void TryOpen_OutOfOrderInsideTheWindow_OpensEachOnce()
    {
        var (sender, receiver, _) = CreatePair();
        var early = sender.Seal([1]);
        var late = sender.Seal([2]);

        Assert.IsTrue(receiver.TryOpen(late, out _));
        Assert.IsTrue(receiver.TryOpen(early, out _));
        Assert.IsFalse(receiver.TryOpen(early, out _));
    }

    [TestMethod]
    public void TryOpen_CounterBehindTheWindow_IsFalse()
    {
        var clock = new ManualTimeProvider();
        using var oldSender = new WireGuardTransportSession(2, 1, KeyA, KeyB, clock);
        using var newSender = new WireGuardTransportSession(2, 1, KeyA, KeyB, clock, WireGuardReplayWindow.WindowSize + 1);
        using var receiver = new WireGuardTransportSession(1, 2, KeyB, KeyA, clock);
        var old = oldSender.Seal([1]);
        Assert.IsTrue(receiver.TryOpen(newSender.Seal([2]), out _));

        Assert.IsFalse(receiver.TryOpen(old, out _));
    }

    [TestMethod]
    public void TryOpen_TamperedOrForAnotherIndexOrNotTransportData_IsFalseAndDoesNotMoveTheWindow()
    {
        var (sender, receiver, _) = CreatePair();
        var datagram = sender.Seal([1]);
        var tampered = (byte[])datagram.Clone();
        tampered[^1] ^= 1;
        var otherIndex = (byte[])datagram.Clone();
        otherIndex[4] ^= 1;

        Assert.IsFalse(receiver.TryOpen(tampered, out _));
        Assert.IsFalse(receiver.TryOpen(otherIndex, out _));
        Assert.IsFalse(receiver.TryOpen(datagram.AsSpan(0, 20), out _));
        Assert.IsTrue(receiver.TryOpen(datagram, out _));
    }

    [TestMethod]
    public void NeedsRekey_AfterRekeyAfterTime_IsTrue()
    {
        var (sender, _, clock) = CreatePair();
        Assert.IsFalse(sender.NeedsRekey);

        clock.Advance(WireGuardTransportSession.RekeyAfterTime);

        Assert.IsTrue(sender.NeedsRekey);
    }

    [TestMethod]
    public void NeedsRekey_AfterRekeyAfterMessages_IsTrue()
    {
        using var sender = new WireGuardTransportSession(1, 2, KeyA, KeyB, new ManualTimeProvider(), WireGuardTransportSession.RekeyAfterMessages);

        Assert.IsTrue(sender.NeedsRekey);
    }

    [TestMethod]
    public void SealAndTryOpen_AfterRejectAfterTime_Refuse()
    {
        var (sender, receiver, clock) = CreatePair();
        var datagram = sender.Seal([1]);

        clock.Advance(WireGuardTransportSession.RejectAfterTime);

        Assert.ThrowsExactly<InvalidOperationException>(() => sender.Seal([1]));
        Assert.IsFalse(receiver.TryOpen(datagram, out _));
    }

    [TestMethod]
    public void Seal_AtRejectAfterMessages_Throws()
    {
        using var sender = new WireGuardTransportSession(1, 2, KeyA, KeyB, new ManualTimeProvider(), WireGuardTransportSession.RejectAfterMessages);

        Assert.ThrowsExactly<InvalidOperationException>(() => sender.Seal([1]));
    }

    [TestMethod]
    public void TryOpen_CounterAtRejectAfterMessages_IsFalse()
    {
        var clock = new ManualTimeProvider();
        using var sender = new WireGuardTransportSession(2, 1, KeyA, KeyB, clock, WireGuardTransportSession.RejectAfterMessages - 1);
        using var receiver = new WireGuardTransportSession(1, 2, KeyB, KeyA, clock);
        var last = sender.Seal([1]);
        var forged = new TransportDataMessage(1, WireGuardTransportSession.RejectAfterMessages, new byte[16]).Encode();

        Assert.IsTrue(receiver.TryOpen(last, out _));
        Assert.IsFalse(receiver.TryOpen(forged, out _));
    }

    private static (WireGuardTransportSession Sender, WireGuardTransportSession Receiver, ManualTimeProvider Clock) CreatePair()
    {
        var clock = new ManualTimeProvider();
        return (new WireGuardTransportSession(2, 1, KeyA, KeyB, clock), new WireGuardTransportSession(1, 2, KeyB, KeyA, clock), clock);
    }
}
