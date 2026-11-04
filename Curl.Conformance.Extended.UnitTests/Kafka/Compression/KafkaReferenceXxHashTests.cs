using System.Text;
using Curl.Conformance.Extended.Kafka.Compression;

namespace Curl.Conformance.Extended.UnitTests.Kafka.Compression;

/// <summary>XXH32 and XXH64 against the published vectors (xxHash's sanity checks and python-xxhash's README).</summary>
[TestClass]
public sealed class KafkaReferenceXxHashTests
{
    private static readonly byte[] Spammish = Encoding.ASCII.GetBytes("Nobody inspects the spammish repetition");

    [TestMethod]
    public void Hash32_PublishedVectors_MatchEach()
    {
        Assert.AreEqual((0x02CC5D05U, 0x32D153FFU, 0xE2293B2FU), (KafkaReferenceXxHash.Hash32([]), KafkaReferenceXxHash.Hash32("abc"u8), KafkaReferenceXxHash.Hash32(Spammish)));
    }

    [TestMethod]
    public void Hash32_InputEndingInOneWholeFourByteGroup_HashesTheGroupAsAWord()
    {
        Assert.AreEqual(0xA3643705U, KafkaReferenceXxHash.Hash32("abcd"u8));
    }

    [TestMethod]
    public void Hash64_PublishedVectors_MatchEach()
    {
        Assert.AreEqual((0xEF46DB3751D8E999UL, 0x44BC2CF5AD770999UL, 0xFBCEA83C8A378BF1UL), (KafkaReferenceXxHash.Hash64([]), KafkaReferenceXxHash.Hash64("abc"u8), KafkaReferenceXxHash.Hash64(Spammish)));
    }
}
