using System.Buffers.Binary;
using System.Text;
using Curl.Original.Protocol.Ws;

namespace Curl.Conformance;

/// <summary>
/// The WebSocket key and masks of upstream's debug build under <c>CURL_ENTROPY</c> (lib/rand.c, <c>randit</c>),
/// which upstream's WebSocket cases (2301 to 2304) set to pin <c>Sec-WebSocket-Key</c> and the frame masks:
/// the first value is the variable's first four bytes read big-endian, each later value is one more, and each
/// value fills up to four bytes of the destination in little-endian order, as <c>Curl_rand_bytes</c> copies an
/// <c>unsigned int</c> on the little-endian machines the cases were recorded on (BL-2688).
/// </summary>
/// <param name="entropy">The value of <c>CURL_ENTROPY</c>.</param>
internal sealed class ForcedEntropyWebSocketRandomSource(string entropy) : IWebSocketRandomSource
{
    private uint nextValue = FirstValue(entropy);

    /// <inheritdoc />
    public void Fill(Span<byte> destination)
    {
        Span<byte> value = stackalloc byte[sizeof(uint)];
        for (int offset = 0; offset < destination.Length; offset += sizeof(uint))
        {
            BinaryPrimitives.WriteUInt32LittleEndian(value, nextValue++);
            value[..Math.Min(sizeof(uint), destination.Length - offset)].CopyTo(destination[offset..]);
        }
    }

    // The seed starts as zero and takes at most the variable's first four bytes, then ntohl reads them big-endian.
    internal static uint FirstValue(string entropy)
    {
        Span<byte> seed = stackalloc byte[sizeof(uint)];
        seed.Clear();
        byte[] entropyBytes = Encoding.Latin1.GetBytes(entropy);
        entropyBytes.AsSpan(0, Math.Min(entropyBytes.Length, sizeof(uint))).CopyTo(seed);
        return BinaryPrimitives.ReadUInt32BigEndian(seed);
    }
}
