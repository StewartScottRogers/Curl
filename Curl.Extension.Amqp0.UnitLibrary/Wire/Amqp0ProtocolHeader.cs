namespace Curl.Extension.Amqp0.Wire;

/// <summary>
/// The protocol header a client sends first (section 4.2.2): "AMQP", 0, then version 0-9-1. A server that does not
/// speak the version answers with its own header and closes; reading any header but this one is a 501 frame-error.
/// </summary>
internal static class Amqp0ProtocolHeader
{
    /// <summary>How many bytes the header is.</summary>
    public const int Length = 8;

    /// <summary>The header's bytes: <c>AMQP\x00\x00\x09\x01</c>.</summary>
    public static ReadOnlySpan<byte> Bytes => "AMQP\0\0\u0009\u0001"u8;

    /// <summary>Checks that <paramref name="received"/> is this header, throwing a 501 frame-error when it is not.</summary>
    public static void Verify(ReadOnlySpan<byte> received)
    {
        if (!received.SequenceEqual(Bytes))
        {
            throw Amqp0WireFormatException.FrameError(
                $"The peer answered with protocol header {Convert.ToHexString(received)}, not AMQP 0-9-1.");
        }
    }
}
