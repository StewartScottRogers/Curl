using Curl.Extension.WireGuard.IpStack;

namespace Curl.Extension.WireGuard.UnitTests.IpStack;

[TestClass]
public sealed class InternetChecksumTests
{
    [TestMethod]
    public void Compute_Rfc1071Example_GivesTheComplementOfTheFoldedSum()
    {
        byte[] data = [0x00, 0x01, 0xF2, 0x03, 0xF4, 0xF5, 0xF6, 0xF7];

        var checksum = InternetChecksum.Compute(data);

        Assert.AreEqual((ushort)0x220D, checksum);
    }

    [TestMethod]
    public void Compute_IPv4HeaderWithItsChecksum_GivesZero()
    {
        byte[] header = Convert.FromHexString("450000730000400040110000C0A80001C0A800C7");
        header[10] = 0xB8;
        header[11] = 0x61;

        var checksum = InternetChecksum.Compute(header);

        Assert.AreEqual((ushort)0, checksum);
    }

    [TestMethod]
    public void Add_OddLength_PadsTheLastByteWithZero()
    {
        var sum = InternetChecksum.Add(0, [0x12, 0x34, 0x56]);

        Assert.AreEqual(0x1234u + 0x5600u, sum);
    }
}
