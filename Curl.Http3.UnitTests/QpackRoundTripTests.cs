using Curl.Http2;
using static Curl.Http3.Qpack;

namespace Curl.Http3;

/// <summary>
/// Runs header lists through a <see cref="QpackEncoder" /> and a <see cref="QpackDecoder" />
/// connected by their encoder and decoder streams, with and without a dynamic table, and
/// checks every list comes back as it went in.
/// </summary>
[TestClass]
public sealed class QpackRoundTripTests
{
    private static readonly HeaderField[][] Requests =
    [
        [
            new(":method", "GET"),
            new(":scheme", "https"),
            new(":authority", "example.com"),
            new(":path", "/index.html"),
            new("user-agent", "curl/8.21.0"),
            new("accept", "*/*"),
        ],
        [
            new(":method", "POST"),
            new(":scheme", "https"),
            new(":authority", "example.com"),
            new(":path", "/upload"),
            new("user-agent", "curl/8.21.0"),
            new("content-type", "application/json"),
            new("authorization", "Bearer secret", IsNeverIndexed: true),
            new("x-empty", string.Empty),
        ],
        [
            new(":method", "GET"),
            new(":scheme", "https"),
            new(":authority", "example.com"),
            new(":path", "/index.html"),
            new("user-agent", "curl/8.21.0"),
            new("cookie", "a=1; b=2"),
            new("x-latin1", "café"),
        ],
    ];

    [TestMethod]
    [DataRow(0L, 0L, 0L, true)]
    [DataRow(4096L, 4096L, 16L, true)]
    [DataRow(4096L, 4096L, 0L, false)]
    [DataRow(200L, 200L, 1L, false)]
    public void HeaderLists_RoundTripThroughEncoderAndDecoder(long maximumTableCapacity, long capacity, long maximumBlockedStreams, bool huffman)
    {
        var encoder = new QpackEncoder(maximumTableCapacity, maximumBlockedStreams, huffman);
        var decoder = new QpackDecoder(maximumTableCapacity, maximumBlockedStreams);
        encoder.TrySetDynamicTableCapacity(capacity);

        for (var round = 0; round < 3; round++)
        {
            for (var request = 0; request < Requests.Length; request++)
            {
                var streamId = (long)((round * Requests.Length) + request) * 4;
                var section = encoder.EncodeFieldSection(streamId, Requests[request]);
                decoder.ReadEncoderStream(encoder.TakeEncoderStreamBytes());

                CollectionAssert.AreEqual(Requests[request], Decode(decoder, streamId, section));
                encoder.ReadDecoderStream(decoder.TakeDecoderStreamBytes());
            }
        }

        Assert.AreEqual(encoder.InsertCount, decoder.InsertCount);
        Assert.AreEqual(encoder.DynamicTableSize, decoder.DynamicTableSize);
    }

    [TestMethod]
    public void DynamicTable_OnceWarm_EncodesARepeatedRequestAsIndexesBelowBase()
    {
        var encoder = new QpackEncoder(4096, 16);
        var decoder = new QpackDecoder(4096, 16);
        encoder.TrySetDynamicTableCapacity(4096);
        var first = encoder.EncodeFieldSection(0, Requests[0]);
        decoder.ReadEncoderStream(encoder.TakeEncoderStreamBytes());
        Decode(decoder, 0, first);
        encoder.ReadDecoderStream(decoder.TakeDecoderStreamBytes());

        var repeat = encoder.EncodeFieldSection(4, Requests[0]);

        CollectionAssert.AreEqual(FromHex("0482 d1 d7 10 11 12 dd"), first);
        CollectionAssert.AreEqual(FromHex("0400 d1 d7 82 81 80 dd"), repeat);
        Assert.IsEmpty(encoder.TakeEncoderStreamBytes());
        CollectionAssert.AreEqual(Requests[0], Decode(decoder, 4, repeat));
    }
}
