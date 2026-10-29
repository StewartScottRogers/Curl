namespace Curl.Extension.Amqp1.Frames;

/// <summary>
/// Writes and reads the eight-byte AMQP 1.0 protocol header (Part 2 section 2.2, Part 5 sections 5.2.1 and 5.3.1):
/// "AMQP", a protocol id (0 AMQP, 2 TLS, 3 SASL) and the version 1.0.0.
/// </summary>
internal static class Amqp1ProtocolHeader
{
    /// <summary>The protocol id of the plain AMQP header <c>AMQP 0 1.0.0</c>.</summary>
    public const byte AmqpProtocolId = 0;

    /// <summary>The protocol id of the TLS header <c>AMQP 2 1.0.0</c>.</summary>
    public const byte TlsProtocolId = 2;

    /// <summary>The protocol id of the SASL header <c>AMQP 3 1.0.0</c>.</summary>
    public const byte SaslProtocolId = 3;

    /// <summary>The length of a protocol header in bytes.</summary>
    public const int Length = 8;

    /// <summary>The header for <paramref name="protocolId"/>, version 1.0.0.</summary>
    public static byte[] Write(byte protocolId) => [(byte)'A', (byte)'M', (byte)'Q', (byte)'P', protocolId, 1, 0, 0];

    /// <summary>
    /// Reads the header in <paramref name="header"/> and returns its protocol id; raises
    /// <see cref="Amqp1MalformedEncodingException"/> unless it is an AMQP 1.0.0 header with protocol id 0, 2 or 3.
    /// </summary>
    public static byte Read(ReadOnlySpan<byte> header)
    {
        if (!IsVersion1_0_0Header(header) || !IsKnownProtocolId(header[4]))
        {
            throw new Amqp1MalformedEncodingException($"{Convert.ToHexString(header)} is not an AMQP 1.0.0 protocol header.");
        }

        return header[4];
    }

    /// <summary>
    /// Reads the header in <paramref name="header"/> and raises <see cref="Amqp1MalformedEncodingException"/>, naming
    /// both, when it is not the header for <paramref name="expectedProtocolId"/>.
    /// </summary>
    public static void RequireProtocol(ReadOnlySpan<byte> header, byte expectedProtocolId)
    {
        var protocolId = Read(header);
        if (protocolId != expectedProtocolId)
        {
            throw new Amqp1MalformedEncodingException(
                $"The peer answered with protocol header AMQP {protocolId} 1.0.0 to AMQP {expectedProtocolId} 1.0.0.");
        }
    }

    private static bool IsVersion1_0_0Header(ReadOnlySpan<byte> header) =>
        header.Length == Length && header.StartsWith("AMQP"u8) && header.EndsWith((ReadOnlySpan<byte>)[1, 0, 0]);

    private static bool IsKnownProtocolId(byte protocolId) => protocolId is AmqpProtocolId or TlsProtocolId or SaslProtocolId;
}
