using Curl.Protocol.Ssh.Keys;
using Curl.Testing;

namespace Curl.Protocol.Ssh.PacketProtection;

[TestClass]
public sealed class SshPlainPacketProtectionTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void SealAndOpen_LeaveThePacketAsItIs()
    {
        using SshPlainPacketProtection protection = new();
        byte[] packet = [0, 0, 0, 12, 4, 21, 0xEE, 0xEE, 0xEE, 0xEE, 1, 2, 3, 4, 5, 6];
        Diagnostics.ArrangePacket("packet", packet);
        Diagnostics.Arrange("sequence number", 9);

        byte[] sealedPacket = protection.Seal(9, packet);
        uint length = protection.DecryptPacketLength(9, sealedPacket[..4]);
        byte[] opened = protection.Open(9, sealedPacket[..4], sealedPacket[4..]);

        Diagnostics.ActBytes("sealed", sealedPacket);
        Diagnostics.Act("decrypted packet length", length);
        Diagnostics.ActBytes("opened", opened);
        Diagnostics.AssertBytes("sealed", packet, sealedPacket);
        Diagnostics.Assert("packet length", 12u, length);
        Diagnostics.AssertBytes("opened", packet[4..], opened);
        Diagnostics.Assert("block size, pads length field, length block, tag", "8, True, 4, 0", $"{protection.BlockSize}, {protection.PadsPacketLengthField}, {protection.LengthBlockLength}, {protection.TagLength}");
        CollectionAssert.AreEqual(packet, sealedPacket);
        Assert.AreEqual(12u, length);
        CollectionAssert.AreEqual(packet[4..], opened);
        Assert.AreEqual(8, protection.BlockSize);
        Assert.IsTrue(protection.PadsPacketLengthField);
        Assert.AreEqual(4, protection.LengthBlockLength);
        Assert.AreEqual(0, protection.TagLength);
    }
}
