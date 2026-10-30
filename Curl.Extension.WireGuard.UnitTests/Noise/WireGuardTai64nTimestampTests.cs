using Curl.Extension.WireGuard.Noise;

namespace Curl.Extension.WireGuard.UnitTests.Noise;

[TestClass]
public sealed class WireGuardTai64nTimestampTests
{
    [TestMethod]
    public void Write_UnixEpochPlusHalfASecond_OffsetsSecondsAndRoundsNanosecondsDown()
    {
        var timestamp = new byte[WireGuardTai64nTimestamp.Size];

        WireGuardTai64nTimestamp.Write(DateTimeOffset.UnixEpoch.AddSeconds(1.5), timestamp);

        // 2^62 + 10 + 1 seconds; 500,000,000 ns rounded down to a multiple of 2^24 is 0x1D000000.
        CollectionAssert.AreEqual(
            new byte[] { 0x40, 0, 0, 0, 0, 0, 0, 0x0B, 0x1D, 0x00, 0x00, 0x00 },
            timestamp);
    }

    [TestMethod]
    public void Write_LaterTime_SortsAfter()
    {
        var earlier = new byte[WireGuardTai64nTimestamp.Size];
        var later = new byte[WireGuardTai64nTimestamp.Size];

        WireGuardTai64nTimestamp.Write(DateTimeOffset.UnixEpoch.AddSeconds(100.9), earlier);
        WireGuardTai64nTimestamp.Write(DateTimeOffset.UnixEpoch.AddSeconds(101), later);

        Assert.IsLessThan(0, earlier.AsSpan().SequenceCompareTo(later));
    }
}
