using Curl.Conformance.Extended.Kafka.Compression.Entropy.Fse;

namespace Curl.Conformance.Extended.Kafka.Compression.Entropy.Sequences;

/// <summary>
/// Writes a zstd compressed block whose sequences are FSE-coded (RFC 8878 section 3.1.1.3.2): a raw literals section,
/// then the sequences section header (Number_of_Sequences in one byte, the Symbol_Compression_Modes byte, and the
/// literals length, offset and match length table descriptions in that order), then the backward bit stream. The
/// decoder reads the bit stream's initial literals length, offset and match length states, then for each sequence its
/// offset extra bits and, unless it is the last, the literals length, match length and offset state updates; the
/// writer writes all of it in the opposite order.
/// </summary>
public static class KafkaReferenceSequencesSectionWriter
{
    /// <summary>The most sequences a one-byte Number_of_Sequences holds.</summary>
    public const int MaximumSequenceCount = 127;

    /// <summary>The most literals a one-byte raw literals section header holds.</summary>
    public const int MaximumLiteralsCount = 31;

    private const int RawLiteralsSizeShift = 3;

    private const int LiteralsLengthModeShift = 6;

    private const int OffsetModeShift = 4;

    private const int MatchLengthModeShift = 2;

    /// <summary>A compressed block's content: <paramref name="literals"/> as a raw literals section, then the sequences section.</summary>
    /// <param name="literals">Every literal the sequences and the trailing literals copy, fewer than 32.</param>
    /// <param name="sequences">The sequences, 1 to <see cref="MaximumSequenceCount"/>.</param>
    /// <param name="literalsLength">How the literals length codes are coded.</param>
    /// <param name="offset">How the offset codes are coded.</param>
    /// <param name="matchLength">How the match length codes are coded.</param>
    /// <returns>The block's content.</returns>
    public static byte[] Block(byte[] literals, IReadOnlyList<KafkaReferenceZstdSequence> sequences, KafkaReferenceSequenceCodeTable literalsLength, KafkaReferenceSequenceCodeTable offset, KafkaReferenceSequenceCodeTable matchLength)
    {
        ArgumentNullException.ThrowIfNull(literals);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(literals.Length, MaximumLiteralsCount, nameof(literals));
        return [(byte)(literals.Length << RawLiteralsSizeShift), .. literals, .. Section(sequences, literalsLength, offset, matchLength)];
    }

    /// <summary>The sequences section of <paramref name="sequences"/>.</summary>
    /// <param name="sequences">The sequences, 1 to <see cref="MaximumSequenceCount"/>.</param>
    /// <param name="literalsLength">How the literals length codes are coded.</param>
    /// <param name="offset">How the offset codes are coded.</param>
    /// <param name="matchLength">How the match length codes are coded.</param>
    /// <returns>The section's bytes.</returns>
    public static byte[] Section(IReadOnlyList<KafkaReferenceZstdSequence> sequences, KafkaReferenceSequenceCodeTable literalsLength, KafkaReferenceSequenceCodeTable offset, KafkaReferenceSequenceCodeTable matchLength)
    {
        ArgumentNullException.ThrowIfNull(sequences);
        ArgumentNullException.ThrowIfNull(literalsLength);
        ArgumentNullException.ThrowIfNull(offset);
        ArgumentNullException.ThrowIfNull(matchLength);
        ArgumentOutOfRangeException.ThrowIfLessThan(sequences.Count, 1, nameof(sequences));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(sequences.Count, MaximumSequenceCount, nameof(sequences));
        foreach (KafkaReferenceZstdSequence sequence in sequences)
        {
            sequence.CheckValid();
        }

        byte modes = (byte)((literalsLength.Mode << LiteralsLengthModeShift) | (offset.Mode << OffsetModeShift) | (matchLength.Mode << MatchLengthModeShift));
        byte[] stream = BitStream(sequences, literalsLength.Table, offset.Table, matchLength.Table);
        return [(byte)sequences.Count, modes, .. literalsLength.Description, .. offset.Description, .. matchLength.Description, .. stream];
    }

    // Literals and match length codes here take no extra bits, so each sequence writes only its offset's.
    private static byte[] BitStream(IReadOnlyList<KafkaReferenceZstdSequence> sequences, KafkaReferenceFseEncodingTable literalsLength, KafkaReferenceFseEncodingTable offset, KafkaReferenceFseEncodingTable matchLength)
    {
        KafkaReferenceFseBackwardBitWriter writer = new();
        KafkaReferenceZstdSequence last = sequences[^1];
        int literalsLengthState = literalsLength.InitialState(last.LiteralsLengthCode);
        int offsetState = offset.InitialState(last.OffsetCode);
        int matchLengthState = matchLength.InitialState(last.MatchLengthCode);
        writer.WriteBits(last.OffsetExtraBits, last.OffsetCode);
        for (int index = sequences.Count - 2; index >= 0; index--)
        {
            KafkaReferenceZstdSequence sequence = sequences[index];
            offsetState = offset.Encode(offsetState, sequence.OffsetCode, writer);
            matchLengthState = matchLength.Encode(matchLengthState, sequence.MatchLengthCode, writer);
            literalsLengthState = literalsLength.Encode(literalsLengthState, sequence.LiteralsLengthCode, writer);
            writer.WriteBits(sequence.OffsetExtraBits, sequence.OffsetCode);
        }

        writer.WriteBits(matchLengthState, matchLength.AccuracyLog);
        writer.WriteBits(offsetState, offset.AccuracyLog);
        writer.WriteBits(literalsLengthState, literalsLength.AccuracyLog);
        return writer.ToArray();
    }
}
