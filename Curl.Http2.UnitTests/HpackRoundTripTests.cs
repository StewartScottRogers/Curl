using Curl.Testing;
using static Curl.Http2.Hpack;

namespace Curl.Http2;

/// <summary>
/// Checks header lists survive <see cref="HpackEncoder" /> then <see cref="HpackDecoder" />
/// across several blocks, with strings that are and are not Huffman-coded.
/// </summary>
[TestClass]
public sealed class HpackRoundTripTests
{
    /// <summary>Gets or sets the MSTest context the diagnostics write to.</summary>
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void EncodeThenDecode_ListsWithAndWithoutHuffman_RoundTripAndKeepTheTablesInStep()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var encoder = new HpackEncoder();
        var decoder = new HpackDecoder();
        HeaderField[][] lists =
        [
            Fields((":status", "200"), ("content-type", "text/html; charset=utf-8"), ("accept", "*/*"), ("x-binary", "ÿþ\u0000\u0001")),
            Fields((":status", "200"), ("content-type", "text/html; charset=utf-8"), ("set-cookie", "id=1"), ("content-length", "42")),
            [new HeaderField("authorization", "Bearer token", IsNeverIndexed: true), new HeaderField("x-secret", "s", IsNeverIndexed: true)],
            Fields(("x-large", new string('q', 4000)), ("", "")),
        ];
        diagnostics.Arrange("list count", lists.Length);

        var listNumber = 0;
        foreach (var list in lists)
        {
            listNumber++;
            diagnostics.Arrange($"list {listNumber} field count", list.Length);
            var encoded = encoder.Encode(list);
            var decoded = decoder.Decode(encoded);
            diagnostics.Act($"list {listNumber} encoded length", encoded.Length);
            diagnostics.Act($"list {listNumber} decoded field count", decoded.Count);
            diagnostics.Act($"list {listNumber} table sizes (encoder, decoder)", $"{encoder.TableSize}, {decoder.TableSize}");

            diagnostics.Assert($"list {listNumber} field count", list.Length, decoded.Count);
            AssertFields(decoded, list);
            diagnostics.Assert($"list {listNumber} table size", encoder.TableSize, decoder.TableSize);
            Assert.AreEqual(encoder.TableSize, decoder.TableSize);
        }
    }

    [TestMethod]
    public void EncodeThenDecode_AfterTheTableShrinks_RoundTrips()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var encoder = new HpackEncoder();
        var decoder = new HpackDecoder();
        var list = Fields(("x-one", "first value"), ("x-two", "second value"), ("x-three", "third value"));
        diagnostics.Arrange("header list", string.Join(", ", list.Select(field => $"{field.Name}: {field.Value}")));
        var firstDecoded = decoder.Decode(encoder.Encode(list));
        diagnostics.Act("first round trip decoded count", firstDecoded.Count);
        AssertFields(firstDecoded, list);

        decoder.SetAllowedMaximumTableSize(100);
        encoder.SetPeerMaximumTableSize(100);
        diagnostics.Arrange("shrunk table limit", 100);

        var decoded = decoder.Decode(encoder.Encode(list));
        diagnostics.Act("decoded header list", string.Join(", ", decoded.Select(field => $"{field.Name}: {field.Value}")));
        diagnostics.Act("table sizes (encoder, decoder)", $"{encoder.TableSize}, {decoder.TableSize}");
        diagnostics.Act("decoder maximum table size", decoder.MaximumTableSize);

        diagnostics.Assert("decoded field count", list.Length, decoded.Count);
        AssertFields(decoded, list);
        diagnostics.Assert("table size", encoder.TableSize, decoder.TableSize);
        Assert.AreEqual(encoder.TableSize, decoder.TableSize);
        diagnostics.Assert("maximum table size", 100, decoder.MaximumTableSize);
        Assert.AreEqual(100, decoder.MaximumTableSize);
    }
}
