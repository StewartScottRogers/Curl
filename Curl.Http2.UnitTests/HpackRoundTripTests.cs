using static Curl.Http2.Hpack;

namespace Curl.Http2;

/// <summary>
/// Checks header lists survive <see cref="HpackEncoder" /> then <see cref="HpackDecoder" />
/// across several blocks, with strings that are and are not Huffman-coded.
/// </summary>
[TestClass]
public sealed class HpackRoundTripTests
{
    [TestMethod]
    public void EncodeThenDecode_ListsWithAndWithoutHuffman_RoundTripAndKeepTheTablesInStep()
    {
        var encoder = new HpackEncoder();
        var decoder = new HpackDecoder();
        HeaderField[][] lists =
        [
            Fields((":status", "200"), ("content-type", "text/html; charset=utf-8"), ("accept", "*/*"), ("x-binary", "ÿþ\u0000\u0001")),
            Fields((":status", "200"), ("content-type", "text/html; charset=utf-8"), ("set-cookie", "id=1"), ("content-length", "42")),
            [new HeaderField("authorization", "Bearer token", IsNeverIndexed: true), new HeaderField("x-secret", "s", IsNeverIndexed: true)],
            Fields(("x-large", new string('q', 4000)), ("", "")),
        ];

        foreach (var list in lists)
        {
            AssertFields(decoder.Decode(encoder.Encode(list)), list);
            Assert.AreEqual(encoder.TableSize, decoder.TableSize);
        }
    }

    [TestMethod]
    public void EncodeThenDecode_AfterTheTableShrinks_RoundTrips()
    {
        var encoder = new HpackEncoder();
        var decoder = new HpackDecoder();
        var list = Fields(("x-one", "first value"), ("x-two", "second value"), ("x-three", "third value"));
        AssertFields(decoder.Decode(encoder.Encode(list)), list);

        decoder.SetAllowedMaximumTableSize(100);
        encoder.SetPeerMaximumTableSize(100);

        AssertFields(decoder.Decode(encoder.Encode(list)), list);
        Assert.AreEqual(encoder.TableSize, decoder.TableSize);
        Assert.AreEqual(100, decoder.MaximumTableSize);
    }
}
