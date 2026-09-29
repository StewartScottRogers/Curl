namespace Curl.Http3;

/// <summary>
/// A <c>GOAWAY</c> frame (RFC 9114 section 7.2.6): the sender is shutting the connection
/// down. From a server, <see cref="Id" /> is a client-initiated bidirectional stream ID;
/// from a client, a push ID.
/// </summary>
/// <param name="id">The stream ID or push ID.</param>
public sealed class Http3GoawayFrame(long id) : Http3Frame
{
    /// <inheritdoc />
    public override Http3FrameType Type => Http3FrameType.Goaway;

    /// <summary>
    /// Gets the stream ID (from a server) or push ID (from a client) from which on requests
    /// or pushes are not processed.
    /// </summary>
    public long Id { get; } = id;

    /// <inheritdoc />
    private protected override void WritePayload(List<byte> payload) => Http3VariableLengthInteger.Write(payload, Id);
}
