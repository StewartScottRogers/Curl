namespace Curl.Conformance.Extended.Coap.BlockwiseObserve;

/// <summary>
/// The options of block-wise transfer (RFC 7959 sections 2 and 4) and Observe (RFC 7641 section 2), with ETag and
/// Max-Age (RFC 7252 section 5.10), written and read as RFC 7252 section 3.2's uint format with the reference
/// server's own code.
/// </summary>
public static class CoapBlockwiseObserveOptions
{
    /// <summary>ETag (4).</summary>
    public const int ETag = 4;

    /// <summary>Observe (6).</summary>
    public const int Observe = 6;

    /// <summary>Max-Age (14).</summary>
    public const int MaxAge = 14;

    /// <summary>Block2 (23).</summary>
    public const int Block2 = 23;

    /// <summary>Block1 (27).</summary>
    public const int Block1 = 27;

    /// <summary>Size2 (28).</summary>
    public const int Size2 = 28;

    /// <summary>Size1 (60).</summary>
    public const int Size1 = 60;

    /// <summary>An option whose value is a uint in as few bytes as possible (none for 0).</summary>
    /// <param name="number">The option number.</param>
    /// <param name="value">The value.</param>
    /// <returns>The option.</returns>
    public static CoapReferenceOption Uint(int number, uint value)
    {
        List<byte> bytes = [];
        for (uint rest = value; rest != 0; rest >>= 8)
        {
            bytes.Insert(0, (byte)rest);
        }

        return new CoapReferenceOption(number, [.. bytes]);
    }

    /// <summary>A Block1 or Block2 option.</summary>
    /// <param name="number"><see cref="Block1" /> or <see cref="Block2" />.</param>
    /// <param name="block">The block value.</param>
    /// <returns>The option.</returns>
    public static CoapReferenceOption Block(int number, CoapReferenceBlock block)
    {
        ArgumentNullException.ThrowIfNull(block);
        return Uint(number, block.ToUint());
    }

    /// <summary>Reads the first option of a number as a uint, leading zero bytes allowed.</summary>
    /// <param name="message">The message.</param>
    /// <param name="number">The option number.</param>
    /// <returns>The value; null when the message has no such option.</returns>
    public static uint? UintOf(CoapReferenceMessage message, int number)
    {
        ArgumentNullException.ThrowIfNull(message);
        CoapReferenceOption? option = message.Options.FirstOrDefault(candidate => candidate.Number == number);
        return option?.Value.Aggregate(0u, (value, next) => (value << 8) | next);
    }

    /// <summary>Reads the first option of a number as a block value.</summary>
    /// <param name="message">The message.</param>
    /// <param name="number"><see cref="Block1" /> or <see cref="Block2" />.</param>
    /// <returns>The block value; null when the message has no such option.</returns>
    public static CoapReferenceBlock? BlockOf(CoapReferenceMessage message, int number) =>
        UintOf(message, number) is { } value ? CoapReferenceBlock.FromUint(value) : null;
}
