using Curl.Extension.CompressionDictionary.Brotli;

namespace Curl.Extension.CompressionDictionary.UnitTests.Brotli;

[TestClass]
public sealed class BrotliBlockSwitchTests
{
    // RFC 7932 section 6: NBLTYPES 3, a type code over symbols 0, 1, 2 and 4 (two bits each), a count code of symbol 0
    // (blocks of 1 to 4), and blocks of 1. Type symbols 4, 1, 0 and 2 then give type 2, the last plus one (0),
    // the second-to-last (2), and type 0.
    [TestMethod]
    public void CountDown_EachSwitchSymbol_GivesSection6Type()
    {
        var writer = new BrotliBitWriter().Bits(1, 1).Bits(1, 3).Bits(0, 1)
            .SimpleCode(5, 0, 1, 2, 4).SimpleCode(26, 0).Bits(0, 2)
            .Bits(3, 2).Bits(0, 2).Bits(2, 2).Bits(0, 2).Bits(0, 2).Bits(0, 2).Bits(1, 2).Bits(0, 2).Bits(0, 8);
        var reader = new BrotliBitReader(writer.ToArray());
        var blocks = BrotliBlockSwitch.Read(reader);
        var types = new List<int>();

        for (var symbol = 0; symbol < 5; symbol++)
        {
            blocks.CountDown(reader);
            types.Add(blocks.CurrentType);
        }

        Assert.AreEqual(3, blocks.TypeCount);
        CollectionAssert.AreEqual(new[] { 0, 2, 0, 2, 0 }, types);
    }
}
