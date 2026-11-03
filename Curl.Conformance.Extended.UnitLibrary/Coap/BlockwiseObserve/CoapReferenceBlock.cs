namespace Curl.Conformance.Extended.Coap.BlockwiseObserve;

/// <summary>A Block1 or Block2 option value as RFC 7959 section 2.2 lays it out: the block number NUM, the M (more) bit and the size exponent SZX.</summary>
/// <param name="Number">The block number, NUM.</param>
/// <param name="More">The M bit: more blocks follow.</param>
/// <param name="SizeExponent">SZX, 0 to 6; the block size is 2 to the power SZX + 4.</param>
public sealed record CoapReferenceBlock(uint Number, bool More, int SizeExponent)
{
    /// <summary>Gets the block size in bytes, 2 to the power <see cref="SizeExponent" /> + 4.</summary>
    public int Size => 1 << (SizeExponent + 4);

    /// <summary>Gets the offset of the block's first byte in the whole body: <see cref="Number" /> times <see cref="Size" />.</summary>
    public int Offset => (int)Number * Size;

    /// <summary>Writes the block value as the uint the option carries: NUM shifted left 4, then M, then SZX.</summary>
    /// <returns>The value.</returns>
    public uint ToUint() => (Number << 4) | (More ? 8u : 0u) | (uint)SizeExponent;

    /// <summary>Reads a block value from the uint an option carries.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The block value.</returns>
    public static CoapReferenceBlock FromUint(uint value) => new(value >> 4, (value & 8) != 0, (int)(value & 7));
}
