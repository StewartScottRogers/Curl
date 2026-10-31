namespace Curl.Extension.Coap.Wire;

/// <summary>
/// The value of a Block1 or Block2 option: the block number NUM, the more flag M and the size
/// exponent SZX, packed as <c>NUM &lt;&lt; 4 | M &lt;&lt; 3 | SZX</c> in 0 to 3 bytes (RFC 7959 section 2.2).
/// </summary>
/// <param name="Number">NUM, the block number, 0 to 2^20 - 1.</param>
/// <param name="More">M, whether more blocks follow.</param>
/// <param name="SizeExponent">SZX, 0 to 6: the block holds <c>2^(SZX + 4)</c> bytes.</param>
internal readonly record struct CoapBlockValue(uint Number, bool More, int SizeExponent)
{
    /// <summary>The largest block number three bytes hold.</summary>
    public const uint MaximumNumber = (1u << 20) - 1;

    /// <summary>The largest size exponent; 7 is reserved.</summary>
    public const int MaximumSizeExponent = 6;

    /// <summary>The block size in bytes, 16 to 1024.</summary>
    public int Size => 1 << (SizeExponent + 4);

    /// <summary>The option value bytes.</summary>
    /// <exception cref="ArgumentOutOfRangeException">NUM is past 2^20 - 1 or SZX past 6.</exception>
    public byte[] Encode()
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(Number, MaximumNumber, nameof(Number));
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)SizeExponent, (uint)MaximumSizeExponent, nameof(SizeExponent));
        uint packed = (Number << 4) | (More ? 8u : 0u) | (uint)SizeExponent;
        return CoapUnsignedInteger.Encode(packed);
    }

    /// <summary>The block value an option's value bytes hold.</summary>
    /// <exception cref="FormatException">The value is longer than 3 bytes or its SZX is the reserved 7.</exception>
    public static CoapBlockValue Decode(ReadOnlySpan<byte> value)
    {
        uint packed = CoapUnsignedInteger.Decode(value, 3);
        int sizeExponent = (int)(packed & 7);
        if (sizeExponent > MaximumSizeExponent)
        {
            throw new FormatException("A CoAP block option's SZX of 7 is reserved.");
        }

        return new CoapBlockValue(packed >> 4, (packed & 8) != 0, sizeExponent);
    }
}
