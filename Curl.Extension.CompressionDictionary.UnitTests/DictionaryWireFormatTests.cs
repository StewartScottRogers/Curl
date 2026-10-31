namespace Curl.Extension.CompressionDictionary.UnitTests;

[TestClass]
public sealed class DictionaryWireFormatTests
{
    private static readonly byte[] Hash = [.. Enumerable.Range(0, 32).Select(value => (byte)value)];

    // RFC 9842 section 2.2: Available-Dictionary is the SHA-256 as an RFC 8941 byte sequence.
    [TestMethod]
    public void BuildAvailableDictionary_Hash_IsColonDelimitedBase64() =>
        Assert.AreEqual(":" + Convert.ToBase64String(Hash) + ":", DictionaryRequestHeaders.BuildAvailableDictionary(Hash));

    [TestMethod]
    public void BuildAvailableDictionary_WrongLength_Throws() =>
        Assert.ThrowsExactly<ArgumentException>(() => DictionaryRequestHeaders.BuildAvailableDictionary(new byte[31]));

    // RFC 9842 section 2.3: Dictionary-ID is the id as an sf-string.
    [TestMethod]
    [DataRow("abc", "\"abc\"")]
    [DataRow("a\"b\\c", "\"a\\\"b\\\\c\"")]
    [DataRow("", null)]
    [DataRow("a\tb", null)]
    [DataRow("a\u007fb", null)]
    public void BuildDictionaryId_Id_IsSfString(string id, string? expected) =>
        Assert.AreEqual(expected, DictionaryRequestHeaders.BuildDictionaryId(id));

    // RFC 9842 sections 4 and 5: dcb and dcz headers carry a magic then the dictionary's SHA-256.
    [TestMethod]
    [DataRow(DictionaryContentEncoding.DictionaryCompressedBrotli, new byte[] { 0xFF, 0x44, 0x43, 0x42 })]
    [DataRow(DictionaryContentEncoding.DictionaryCompressedZstandard, new byte[] { 0x5E, 0x2A, 0x4D, 0x18, 0x20, 0x00, 0x00, 0x00 })]
    public void Check_StreamHeader_ReportsEachOutcome(DictionaryContentEncoding encoding, byte[] magic)
    {
        byte[] stream = [.. magic, .. Hash, 0x01];
        byte[] otherHash = [.. Hash.Reverse()];
        byte[] wrongMagic = [.. stream];
        wrongMagic[1] ^= 0xFF;

        Assert.AreEqual(magic.Length + 32, DictionaryStreamHeader.LengthOf(encoding));
        Assert.AreEqual(DictionaryStreamHeaderCheck.Matches, DictionaryStreamHeader.Check(encoding, stream, Hash));
        Assert.AreEqual(DictionaryStreamHeaderCheck.WrongDictionary, DictionaryStreamHeader.Check(encoding, stream, otherHash));
        Assert.AreEqual(DictionaryStreamHeaderCheck.WrongMagic, DictionaryStreamHeader.Check(encoding, wrongMagic, Hash));
        Assert.AreEqual(DictionaryStreamHeaderCheck.Incomplete, DictionaryStreamHeader.Check(encoding, stream.AsSpan(0, magic.Length + 31), Hash));
    }
}
