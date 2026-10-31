using Curl.Extension.CompressionDictionary.Brotli;

namespace Curl.Extension.CompressionDictionary.UnitTests.Brotli;

[TestClass]
public sealed class BrotliPrefixCodeReaderTests
{
    private static BrotliBitReader ReaderOf(BrotliBitWriter writer) => new(writer.Bits(0, 64).ToArray());

    // RFC 7932 section 3.4: a simple code may not repeat a symbol or name one past its alphabet.
    [TestMethod]
    [DataRow(4, 1, 1)]
    [DataRow(3, 3, 0)]
    public void Read_BadSimpleCode_Throws(int alphabetSize, int first, int second) =>
        Assert.ThrowsExactly<BrotliFormatException>(() => BrotliPrefixCodeReader.Read(ReaderOf(new BrotliBitWriter().SimpleCode(alphabetSize, first, second)), alphabetSize));

    // NSYM 4 with tree-select 1 gives lengths 1, 2, 3, 3: codes 0, 01, 011, 111 read right to left.
    [TestMethod]
    public void Read_SimpleCodeTreeSelect1_DecodesEachLength()
    {
        var reader = ReaderOf(new BrotliBitWriter().Bits(1, 2).Bits(3, 2).Bits(2, 2).Bits(0, 2).Bits(3, 2).Bits(1, 2).Bits(1, 1).Bits(0, 1).Bits(1, 2).Bits(3, 3).Bits(7, 3));

        var code = BrotliPrefixCodeReader.Read(reader, 4);

        CollectionAssert.AreEqual(new[] { 2, 0, 1, 3 }, new[] { code.Decode(reader), code.Decode(reader), code.Decode(reader), code.Decode(reader) });
    }

    // RFC 7932 section 3.5: all code length code lengths zero is not a complete code.
    [TestMethod]
    public void Read_EmptyCodeLengthCode_Throws() =>
        Assert.ThrowsExactly<BrotliFormatException>(() => BrotliPrefixCodeReader.Read(ReaderOf(new BrotliBitWriter().Bits(0, 2).Bits(0, 36)), 4));

    // HSKIP 2 starts at symbol 3; four lengths of 3 fill half the code space.
    [TestMethod]
    public void Read_IncompleteSymbolLengths_Throws() =>
        Assert.ThrowsExactly<BrotliFormatException>(() => BrotliPrefixCodeReader.Read(ReaderOf(new BrotliBitWriter().Bits(2, 2).Bits(2, 2).Bits(0, 30)), 4));

    // HSKIP 3, then lengths for 4, 0 and 5 zero and code 17 the only one: it repeats zero 3 times into a 2-symbol alphabet.
    [TestMethod]
    public void Read_RepeatPastAlphabet_Throws() =>
        Assert.ThrowsExactly<BrotliFormatException>(() => BrotliPrefixCodeReader.Read(ReaderOf(new BrotliBitWriter().Bits(3, 2).Bits(0, 6).Bits(1, 2).Bits(0, 22)), 2));

    // Code length code 1111 (length 5) for symbol 1 alone makes a zero-bit code, so every symbol gets length 1.
    [TestMethod]
    public void Read_CodeLengthCodeOfLengthFive_GivesLengthOneCode()
    {
        var reader = ReaderOf(new BrotliBitWriter().Bits(0, 2).Bits(15, 4).Bits(0, 34).Bits(2, 2));

        var code = BrotliPrefixCodeReader.Read(reader, 4);

        CollectionAssert.AreEqual(new[] { 0, 1 }, new[] { code.Decode(reader), code.Decode(reader) });
    }
}
