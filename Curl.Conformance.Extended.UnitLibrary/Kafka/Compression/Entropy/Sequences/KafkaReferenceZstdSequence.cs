using System.Numerics;

namespace Curl.Conformance.Extended.Kafka.Compression.Entropy.Sequences;

/// <summary>
/// One zstd sequence (RFC 8878 section 3.1.1.3.2.1): copy <see cref="LiteralsLength"/> literals, then
/// <see cref="MatchLength"/> bytes from <see cref="Offset"/> bytes back. Its literals length (0 to 15) and match length
/// (3 to 34) are codes of their own with no extra bits, and its offset is written as an Offset_Value above 3, so no
/// repeat offset is used.
/// </summary>
/// <param name="LiteralsLength">The literals copied first, 0 to <see cref="MaximumLiteralsLength"/>.</param>
/// <param name="Offset">How far back the match starts, at least 1.</param>
/// <param name="MatchLength">The bytes the match copies, <see cref="MinimumMatchLength"/> to <see cref="MaximumMatchLength"/>.</param>
public readonly record struct KafkaReferenceZstdSequence(int LiteralsLength, int Offset, int MatchLength)
{
    /// <summary>The largest literals length whose code is the length itself.</summary>
    public const int MaximumLiteralsLength = 15;

    /// <summary>The smallest match length; its code is 0.</summary>
    public const int MinimumMatchLength = 3;

    /// <summary>The largest match length whose code is the length less 3.</summary>
    public const int MaximumMatchLength = 34;

    // Offset_Value 1 to 3 names a repeat offset; a new offset is written as Offset_Value less 3.
    private const int RepeatOffsetValues = 3;

    /// <summary>The literals length code: the length itself.</summary>
    public byte LiteralsLengthCode => (byte)LiteralsLength;

    /// <summary>The match length code: the length less <see cref="MinimumMatchLength"/>.</summary>
    public byte MatchLengthCode => (byte)(MatchLength - MinimumMatchLength);

    /// <summary>The offset code: the highest set bit of the Offset_Value, which is also how many extra bits follow it.</summary>
    public byte OffsetCode => (byte)BitOperations.Log2((uint)OffsetValue);

    /// <summary>The offset's extra bits: the Offset_Value less its highest set bit.</summary>
    public int OffsetExtraBits => OffsetValue - (1 << OffsetCode);

    private int OffsetValue => Offset + RepeatOffsetValues;

    /// <summary>Throws unless each length is in the range its code covers and the offset is at least 1.</summary>
    public void CheckValid()
    {
        ArgumentOutOfRangeException.ThrowIfNegative(LiteralsLength, nameof(LiteralsLength));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(LiteralsLength, MaximumLiteralsLength, nameof(LiteralsLength));
        ArgumentOutOfRangeException.ThrowIfLessThan(Offset, 1, nameof(Offset));
        ArgumentOutOfRangeException.ThrowIfLessThan(MatchLength, MinimumMatchLength, nameof(MatchLength));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(MatchLength, MaximumMatchLength, nameof(MatchLength));
    }
}
