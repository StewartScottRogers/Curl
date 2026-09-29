namespace Curl.Http3;

/// <summary>
/// A <c>MAX_PUSH_ID</c> frame (RFC 9114 section 7.2.7): the largest push ID the client
/// allows the server to use. Only a client sends one; curl never does.
/// </summary>
/// <param name="pushId">The largest push ID allowed.</param>
public sealed class Http3MaxPushIdFrame(long pushId) : Http3Frame
{
    /// <inheritdoc />
    public override Http3FrameType Type => Http3FrameType.MaxPushId;

    /// <summary>Gets the largest push ID allowed.</summary>
    public long PushId { get; } = pushId;

    /// <inheritdoc />
    private protected override void WritePayload(List<byte> payload) => Http3VariableLengthInteger.Write(payload, PushId);
}
