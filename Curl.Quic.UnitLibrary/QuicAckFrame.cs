namespace Curl.Quic;

/// <summary>
/// An ACK frame (RFC 9000 section 19.3): the packets received, as descending ranges.
/// </summary>
/// <param name="LargestAcknowledged">The largest packet number acknowledged.</param>
/// <param name="AckDelay">The encoded delay since that packet arrived, before the ack_delay_exponent is applied.</param>
/// <param name="FirstAckRange">How many packets below <paramref name="LargestAcknowledged" /> are acknowledged with it.</param>
/// <param name="AckRanges">The further ranges, each below the one before.</param>
/// <param name="EcnCounts">The ECN counts, which make the frame type 0x03, or <see langword="null" /> for type 0x02.</param>
public sealed record QuicAckFrame(ulong LargestAcknowledged, ulong AckDelay, ulong FirstAckRange, IReadOnlyList<QuicAckRange> AckRanges, QuicEcnCounts? EcnCounts) : QuicFrame
{
    /// <inheritdoc />
    public override QuicFrameType Type => EcnCounts is null ? QuicFrameType.Ack : QuicFrameType.AckWithEcnCounts;

    /// <inheritdoc />
    private protected override void WriteFieldsTo(QuicWriter writer)
    {
        writer.WriteVariableLengthInteger(LargestAcknowledged);
        writer.WriteVariableLengthInteger(AckDelay);
        writer.WriteVariableLengthInteger((ulong)AckRanges.Count);
        writer.WriteVariableLengthInteger(FirstAckRange);
        foreach (var range in AckRanges)
        {
            writer.WriteVariableLengthInteger(range.Gap);
            writer.WriteVariableLengthInteger(range.Length);
        }

        if (EcnCounts is { } counts)
        {
            writer.WriteVariableLengthInteger(counts.Ect0);
            writer.WriteVariableLengthInteger(counts.Ect1);
            writer.WriteVariableLengthInteger(counts.EcnCe);
        }
    }
}
