namespace Curl.Http3;

/// <summary>
/// A <c>CANCEL_PUSH</c> frame (RFC 9114 section 7.2.3): the server push with
/// <see cref="PushId" /> is cancelled.
/// </summary>
/// <param name="pushId">The push ID of the server push.</param>
public sealed class Http3CancelPushFrame(long pushId) : Http3Frame
{
    /// <inheritdoc />
    public override Http3FrameType Type => Http3FrameType.CancelPush;

    /// <summary>Gets the push ID of the cancelled server push.</summary>
    public long PushId { get; } = pushId;

    /// <inheritdoc />
    private protected override void WritePayload(List<byte> payload) => Http3VariableLengthInteger.Write(payload, PushId);
}
