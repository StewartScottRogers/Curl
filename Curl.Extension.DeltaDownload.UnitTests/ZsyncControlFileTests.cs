using System.Security.Cryptography;
using System.Text;

namespace Curl.Extension.DeltaDownload.UnitTests;

[TestClass]
public sealed class ZsyncControlFileTests
{
    private static readonly byte[] Target = Encoding.ASCII.GetBytes("the quick brown fox jumps");

    [TestMethod]
    public void Parse_AControlFile_ReadsItsHeadersAndOneChecksumPerBlock_Zsync06HeadersAndBlockChecksums()
    {
        var control = ZsyncControlFile.Parse(ZsyncFile.For(Target, 8, "2,3,5"))!;

        Assert.AreEqual(8, control.BlockSize);
        Assert.AreEqual(25, control.Length);
        Assert.AreEqual(4, control.BlockCount);
        Assert.AreEqual(2, control.SequenceMatches);
        Assert.AreEqual(3, control.WeakChecksumBytes);
        Assert.AreEqual(5, control.StrongChecksumBytes);
        Assert.AreEqual("file.bin", control.FileName);
        CollectionAssert.AreEqual(SHA1.HashData(Target), control.Sha1);
        var first = RollingChecksum.Of(Target.AsSpan(0, 8));
        Assert.AreEqual(first.Value & 0xFFFFFF, control.WeakChecksum(0));
        Assert.AreEqual(control.WeakChecksum(0), control.Truncate(first));
        CollectionAssert.AreEqual(Md4.Hash(Target.AsSpan(0, 8))[..5], control.StrongChecksum(0).ToArray());
    }

    [TestMethod]
    public void Parse_NoHashLengthsHeader_TakesTheDefault1_4_16_Zsync06HashLengths()
    {
        var text = Encoding.Latin1.GetString(ZsyncFile.For(Target, 32)).Replace("Hash-Lengths: 1,4,16\n", string.Empty, StringComparison.Ordinal);

        var control = ZsyncControlFile.Parse(Encoding.Latin1.GetBytes(text))!;

        Assert.AreEqual((1, 4, 16), (control.SequenceMatches, control.WeakChecksumBytes, control.StrongChecksumBytes));
        Assert.AreEqual(RollingChecksum.Of(Target.Concat(new byte[7]).ToArray()).Value, control.Truncate(RollingChecksum.Of(Target.Concat(new byte[7]).ToArray())));
        Assert.IsNull(ZsyncControlFile.Parse(ZsyncFile.For(Target, 32, fileName: null))!.FileName);
    }

    [TestMethod]
    [DataRow("no-empty-line")]
    [DataRow("zsync: 0.6.2\n", "")]
    [DataRow("Blocksize: 8\n", "Blocksize: eight\n")]
    [DataRow("Blocksize: 8\n", "Blocksize: 0\n")]
    [DataRow("Hash-Lengths: 1,4,16\n", "Hash-Lengths: 1,4\n")]
    [DataRow("Hash-Lengths: 1,4,16\n", "Hash-Lengths: 3,4,16\n")]
    [DataRow("Hash-Lengths: 1,4,16\n", "Hash-Lengths: 1,5,16\n")]
    [DataRow("Hash-Lengths: 1,4,16\n", "Hash-Lengths: 1,0,16\n")]
    [DataRow("Hash-Lengths: 1,4,16\n", "Hash-Lengths: 1,4,2\n")]
    [DataRow("Hash-Lengths: 1,4,16\n", "Hash-Lengths: 1,4,17\n")]
    [DataRow("Length: 25\n", "Length: 33\n")]
    [DataRow("Length: 25\n", "Length: -1\n")]
    [DataRow("SHA-1: ", "SHA-1: 0")]
    [DataRow("SHA-1: ", "Sha: ")]
    public void Parse_ABrokenControlFile_GivesNone(string field, string? replacement = null)
    {
        var text = Encoding.Latin1.GetString(ZsyncFile.For(Target, 8));
        text = replacement is null ? text.Replace("\n\n", "\n", StringComparison.Ordinal) : text.Replace(field, replacement, StringComparison.Ordinal);
        Assert.IsNull(ZsyncControlFile.Parse(Encoding.Latin1.GetBytes(text)));
    }

    [TestMethod]
    public void Parse_ASha1ThatIsNotHexadecimal_GivesNone()
    {
        var text = Encoding.Latin1.GetString(ZsyncFile.For(Target, 8));
        var at = text.IndexOf("SHA-1: ", StringComparison.Ordinal) + 7;

        Assert.IsNull(ZsyncControlFile.Parse(Encoding.Latin1.GetBytes(text[..at] + "zz" + text[(at + 2)..])));
    }
}
