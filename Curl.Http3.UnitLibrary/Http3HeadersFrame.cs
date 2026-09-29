namespace Curl.Http3;

/// <summary>
/// A <c>HEADERS</c> frame (RFC 9114 section 7.2.2): one QPACK-encoded field section.
/// </summary>
/// <param name="encodedFieldSection">The field section as <see cref="QpackEncoder" /> encodes it.</param>
public sealed class Http3HeadersFrame(ReadOnlyMemory<byte> encodedFieldSection) : Http3Frame
{
    /// <inheritdoc />
    public override Http3FrameType Type => Http3FrameType.Headers;

    /// <summary>Gets the QPACK-encoded field section.</summary>
    public ReadOnlyMemory<byte> EncodedFieldSection { get; } = encodedFieldSection;

    /// <inheritdoc />
    private protected override void WritePayload(List<byte> payload) => payload.AddRange(EncodedFieldSection.Span);
}
