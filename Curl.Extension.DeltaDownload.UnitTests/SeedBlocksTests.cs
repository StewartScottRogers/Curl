using System.Text;

namespace Curl.Extension.DeltaDownload.UnitTests;

[TestClass]
public sealed class SeedBlocksTests
{
    private static readonly byte[] Target = Encoding.ASCII.GetBytes("AAAABBBBCCCCDDDDEE");

    [TestMethod]
    public void Find_BlocksAnywhereInTheSeed_FindsThemAndLeavesTheRestAsMergedRanges_Zsync06BlockChecksums()
    {
        var blocks = SeedBlocks.Find(Control("1,4,16"), "xxAAAAzCCCCDDDDEE"u8);

        CollectionAssert.AreEqual(new[] { true, false, true, true, true }, blocks.Found);
        CollectionAssert.AreEqual(new[] { (4L, 7L) }, blocks.MissingRanges().ToArray());
        Assert.AreEqual("AAAA", Encoding.ASCII.GetString(blocks.Target, 0, 4));
        Assert.AreEqual("CCCCDDDDEE\0\0", Encoding.ASCII.GetString(blocks.Target, 8, 12));
    }

    [TestMethod]
    public void Find_NoSeed_LeavesTheWholeFileAsOneRangeEndingAtItsLength()
    {
        var blocks = SeedBlocks.Find(Control("1,4,16"), []);

        CollectionAssert.AreEqual(new[] { (0L, 17L) }, blocks.MissingRanges().ToArray());
    }

    [TestMethod]
    public void Find_SequenceMatchesOf2_TakesABlockOnlyWhenTheNextFollowsIt_Zsync06HashLengths()
    {
        var blocks = SeedBlocks.Find(Control("2,4,16"), "AAAAxBBBBCCCCzzDDDDEE"u8);

        CollectionAssert.AreEqual(new[] { false, true, false, true, true }, blocks.Found);
        CollectionAssert.AreEqual(new[] { (0L, 3L), (8L, 11L) }, blocks.MissingRanges().ToArray());
    }

    [TestMethod]
    public void Find_SequenceMatchesOf2_TheSeedEndingAfterABlock_DoesNotTakeIt()
    {
        var control = ZsyncControlFile.Parse(ZsyncFile.For("AA\0\0BBBB"u8.ToArray(), 4, "2,4,16"))!;

        var blocks = SeedBlocks.Find(control, "AA"u8);

        Assert.IsFalse(blocks.Found[0]);
    }

    [TestMethod]
    public void Find_AWindowWhoseWeakChecksumMatchesButNotItsMd4_DoesNotTakeTheBlock_Zsync06BlockChecksums()
    {
        // "@BB@" has the byte sum and weighted sum of "AAAA", so only the MD4 checksum tells them apart.
        Assert.AreEqual(RollingChecksum.Of("AAAA"u8), RollingChecksum.Of("@BB@"u8));

        var blocks = SeedBlocks.Find(Control("1,4,16"), "@BB@"u8);

        Assert.IsFalse(blocks.Found[0]);
    }

    [TestMethod]
    public void Find_TwoBlocksAlike_TakesBothFromOneWindow()
    {
        var control = ZsyncControlFile.Parse(ZsyncFile.For(Encoding.ASCII.GetBytes("ABCDABCD"), 4))!;

        var blocks = SeedBlocks.Find(control, "ABCD"u8);

        CollectionAssert.AreEqual(new[] { true, true }, blocks.Found);
        Assert.AreEqual(0, blocks.MissingRanges().Count);
    }

    private static ZsyncControlFile Control(string hashLengths) => ZsyncControlFile.Parse(ZsyncFile.For(Target, 4, hashLengths))!;
}
