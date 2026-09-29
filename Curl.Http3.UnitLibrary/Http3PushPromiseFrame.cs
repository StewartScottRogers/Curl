namespace Curl.Http3;

/// <summary>
/// A <c>PUSH_PROMISE</c> frame (RFC 9114 section 7.2.5): a push ID and the QPACK-encoded
/// request of the server push it promises.
/// </summary>
/// <param name="pushId">The push ID.</param>
/// <param name="encodedFieldSection">The promised request's QPACK-encoded field section.</param>
public sealed class Http3PushPromiseFrame(long pushId, ReadOnlyMemory<byte> encodedFieldSection) : Http3Frame
{
    /// <inheritdoc />
    public override Http3FrameType Type => Http3FrameType.PushPromise;

    /// <summary>Gets the push ID.</summary>
    public long PushId { get; } = pushId;

    /// <summary>Gets the promised request's QPACK-encoded field section.</summary>
    public ReadOnlyMemory<byte> EncodedFieldSection { get; } = encodedFieldSection;

    /// <summary>
    /// Interprets a <c>PUSH_PROMISE</c> payload.
    /// </summary>
    /// <param name="payload">The whole payload.</param>
    /// <returns>The frame.</returns>
    internal static Http3PushPromiseFrame ParsePayload(ReadOnlySpan<byte> payload)
    {
        var position = 0;
        var pushId = ReadInteger(payload, ref position, Http3FrameType.PushPromise);
        return new Http3PushPromiseFrame(pushId, payload[position..].ToArray());
    }

    /// <inheritdoc />
    private protected override void WritePayload(List<byte> payload)
    {
        Http3VariableLengthInteger.Write(payload, PushId);
        payload.AddRange(EncodedFieldSection.Span);
    }
}
