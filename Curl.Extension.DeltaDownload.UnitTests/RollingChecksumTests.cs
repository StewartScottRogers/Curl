using System.Text;

namespace Curl.Extension.DeltaDownload.UnitTests;

[TestClass]
public sealed class RollingChecksumTests
{
    [TestMethod]
    public void Of_ABlock_SumsItsBytesAndWeighsEachByItsDistanceFromTheEnd_ZsyncWeakChecksum()
    {
        var checksum = RollingChecksum.Of("abc"u8);

        Assert.AreEqual(97 + 98 + 99, checksum.A);
        Assert.AreEqual((3 * 97) + (2 * 98) + 99, checksum.B);
        Assert.AreEqual(((uint)checksum.A << 16) | checksum.B, checksum.Value);
    }

    [TestMethod]
    public void Roll_EveryWindowAlongALongText_EqualsTheChecksumOfThatWindow_WrappingModulo65536()
    {
        var text = Encoding.Latin1.GetBytes(string.Concat(Enumerable.Range(0, 400).Select(index => (char)(index * 37 % 256))));
        const int size = 300;
        var rolled = RollingChecksum.Of(text.AsSpan(0, size));

        for (var offset = 1; offset + size <= text.Length; offset++)
        {
            rolled = rolled.Roll(text[offset - 1], text[offset + size - 1], size);
            Assert.AreEqual(RollingChecksum.Of(text.AsSpan(offset, size)), rolled);
        }
    }
}
