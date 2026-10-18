using System.Text;

namespace Curl.Conformance;

/// <summary>
/// Pins <see cref="RtspInterleavedPackets"/> to upstream's <c>rtspd</c> at <c>curl-8_22_0</c>: one packet per
/// <c>rtp:</c> line naming the part, its length lying by <c>size_err</c>, its payload repeating <c>$_1234\n\0Rsdf</c>.
/// </summary>
[TestClass]
public sealed class RtspInterleavedPacketsTests
{
    [TestMethod]
    public void ForPart_BuildsOnePacketPerLineNamingThePartInFileOrder()
    {
        byte[] commands = "rtp: part 2 channel 1 size 14\nrtp: part 3 channel 0 size 1\nidle\nrtp: part 2 channel 0 size 2 size_err -1\n"u8.ToArray();

        byte[] packets = RtspInterleavedPackets.ForPart(commands, 2);

        Assert.AreEqual("$\u0001\u0000\u000e$_1234\n\0Rsdf$_" + "$\u0000\u0000\u0001$_", Encoding.Latin1.GetString(packets));
    }

    [TestMethod]
    public void ForPart_SizeErrorAndWideNumbers_AreCodedInNetworkOrder()
    {
        byte[] packets = RtspInterleavedPackets.ForPart("rtp: part 0 channel 257 size 300 size_err 11"u8.ToArray(), 0);

        Assert.AreEqual(304, packets.Length);
        CollectionAssert.AreEqual(new byte[] { (byte)'$', 1, 1, 55 }, packets[..4]);
    }

    [TestMethod]
    public void ForPart_NoLineNamesThePart_IsEmpty()
    {
        byte[] packets = RtspInterleavedPackets.ForPart("rtp: part 4 channel 0 size 2\nrtp: part x\n"u8.ToArray(), 1);

        Assert.IsEmpty(packets);
    }
}
