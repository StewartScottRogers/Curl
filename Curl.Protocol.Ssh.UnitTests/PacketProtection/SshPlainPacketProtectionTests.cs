namespace Curl.Protocol.Ssh.PacketProtection;

[TestClass]
public sealed class SshPlainPacketProtectionTests
{
    [TestMethod]
    public void SealAndOpen_LeaveThePacketAsItIs()
    {
        using SshPlainPacketProtection protection = new();
        byte[] packet = [0, 0, 0, 12, 4, 21, 0xEE, 0xEE, 0xEE, 0xEE, 1, 2, 3, 4, 5, 6];

        byte[] sealedPacket = protection.Seal(9, packet);
        uint length = protection.DecryptPacketLength(9, sealedPacket[..4]);
        byte[] opened = protection.Open(9, sealedPacket[..4], sealedPacket[4..]);

        CollectionAssert.AreEqual(packet, sealedPacket);
        Assert.AreEqual(12u, length);
        CollectionAssert.AreEqual(packet[4..], opened);
        Assert.AreEqual(8, protection.BlockSize);
        Assert.IsTrue(protection.PadsPacketLengthField);
        Assert.AreEqual(4, protection.LengthBlockLength);
        Assert.AreEqual(0, protection.TagLength);
    }
}
