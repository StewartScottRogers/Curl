using System.Buffers.Binary;

namespace Curl.Ntlm;

/// <summary>
/// The pieces every NTLM message shares (MS-NLMP section 2.2.1): the <c>NTLMSSP\0</c>
/// signature, the message type after it, and the 8-byte security buffer (length, allocated
/// length, offset) that points into the payload.
/// </summary>
internal static class NtlmMessageLayout
{
    /// <summary>The length of a security buffer field in bytes.</summary>
    public const int SecurityBufferSize = 8;

    /// <summary>Writes <c>NTLMSSP\0</c> and the little-endian <paramref name="messageType" /> at the start of <paramref name="message" />.</summary>
    public static void WriteSignatureAndType(Span<byte> message, uint messageType)
    {
        "NTLMSSP\0"u8.CopyTo(message);
        BinaryPrimitives.WriteUInt32LittleEndian(message[8..], messageType);
    }

    /// <summary>Whether <paramref name="message" /> starts with <c>NTLMSSP\0</c> and <paramref name="messageType" />.</summary>
    public static bool HasSignatureAndType(ReadOnlySpan<byte> message, uint messageType) =>
        message.StartsWith("NTLMSSP\0"u8) && BinaryPrimitives.ReadUInt32LittleEndian(message[8..]) == messageType;

    /// <summary>
    /// Writes a security buffer at the start of <paramref name="field" />: the length twice
    /// (as length and allocated length), then the offset, as curl writes it.
    /// </summary>
    public static void WriteSecurityBuffer(Span<byte> field, int length, int offset)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(field, (ushort)length);
        BinaryPrimitives.WriteUInt16LittleEndian(field[2..], (ushort)length);
        BinaryPrimitives.WriteUInt32LittleEndian(field[4..], (uint)offset);
    }
}
