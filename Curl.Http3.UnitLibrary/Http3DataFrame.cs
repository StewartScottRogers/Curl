namespace Curl.Http3;

/// <summary>
/// A <c>DATA</c> frame (RFC 9114 section 7.2.1): request or response content.
/// </summary>
/// <param name="payload">The content bytes.</param>
public sealed class Http3DataFrame(ReadOnlyMemory<byte> payload) : Http3Frame
{
    /// <inheritdoc />
    public override Http3FrameType Type => Http3FrameType.Data;

    /// <summary>Gets the content bytes.</summary>
    public ReadOnlyMemory<byte> Payload { get; } = payload;

    /// <inheritdoc />
    private protected override void WritePayload(List<byte> payload) => payload.AddRange(Payload.Span);
}
