namespace Curl.Quic;

/// <summary>
/// A PATH_CHALLENGE frame (RFC 9000 section 19.17): eight bytes the peer echoes in a PATH_RESPONSE.
/// </summary>
/// <param name="Data">The eight bytes.</param>
public sealed record QuicPathChallengeFrame(ReadOnlyMemory<byte> Data) : QuicFrame
{
    /// <summary>The length of a path challenge's data.</summary>
    public const int DataLength = 8;

    /// <inheritdoc />
    public override QuicFrameType Type => QuicFrameType.PathChallenge;

    /// <inheritdoc />
    private protected override void WriteFieldsTo(QuicWriter writer) => writer.WriteBytes(Data.Span);
}
