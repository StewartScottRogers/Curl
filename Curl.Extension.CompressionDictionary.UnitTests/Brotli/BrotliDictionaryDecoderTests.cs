using System.Security.Cryptography;
using System.Text;
using Curl.Extension.CompressionDictionary.Brotli;

namespace Curl.Extension.CompressionDictionary.UnitTests.Brotli;

[TestClass]
public sealed class BrotliDictionaryDecoderTests
{
    private static string Sha256Of(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    // RFC 7932 Appendix A: the static dictionary is 122,784 bytes with this SHA-256.
    [TestMethod]
    public void StaticDictionary_IsAppendixA() =>
        Assert.AreEqual("20e42eb1b511c21806d4d227d07e5dd06877d8ce7b3a817f378f313653f35c70", Sha256Of(BrotliStaticDictionary.Data.ToArray()));

    // RFC 9842 section 4 and RFC 7932 section 4: distances past the decoded bytes reach into the prefix dictionary.
    [TestMethod]
    public void Decode_PrefixDictionaryWindow24_GivesTheOriginal() =>
        Assert.AreEqual(BrotliRecordedStreams.WithDictionarySha256, Sha256Of(BrotliDictionaryDecoder.Decode(BrotliRecordedStreams.WithDictionaryQuality11Window24, BrotliRecordedStreams.Dictionary)));

    // RFC 7932 section 9.1: with a 1 KiB window, the dictionary starts where the window ends, not where the output starts.
    [TestMethod]
    public void Decode_PrefixDictionaryWindow10_GivesTheOriginal() =>
        Assert.AreEqual(BrotliRecordedStreams.WithDictionarySha256, Sha256Of(BrotliDictionaryDecoder.Decode(BrotliRecordedStreams.WithDictionaryQuality5Window10, BrotliRecordedStreams.Dictionary)));

    [TestMethod]
    public void Decode_PrefixDictionaryStreamWithoutTheDictionary_Throws() =>
        Assert.ThrowsExactly<BrotliFormatException>(() => BrotliDictionaryDecoder.Decode(BrotliRecordedStreams.WithDictionaryQuality11Window24));

    // RFC 7932 sections 6, 7 and 8: block types, context modeling and static dictionary references with transforms.
    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void Decode_TextWithoutDictionary_GivesTheOriginal(bool quality11) =>
        Assert.AreEqual(BrotliRecordedStreams.StaticSha256, Sha256Of(BrotliDictionaryDecoder.Decode(quality11 ? BrotliRecordedStreams.StaticQuality11Window22 : BrotliRecordedStreams.StaticQuality1Window18)));

    // RFC 7932 section 8: uppercase transforms of static dictionary words.
    [TestMethod]
    public void Decode_PhraseOfDictionaryWords_GivesThePhrase() =>
        Assert.AreEqual(BrotliRecordedStreams.Phrase, Encoding.UTF8.GetString(BrotliDictionaryDecoder.Decode(BrotliRecordedStreams.PhraseQuality11Window10)));

    // RFC 7932 section 9.2: incompressible input is stored in uncompressed meta-blocks.
    [TestMethod]
    public void Decode_RandomBytes_GivesTheOriginal() =>
        Assert.AreEqual(BrotliRecordedStreams.RandomSha256, Sha256Of(BrotliDictionaryDecoder.Decode(BrotliRecordedStreams.RandomQuality11Window16)));
}
