using System.Buffers.Binary;
using System.Text;

namespace Curl.Protocol.Smb;

/// <summary>
/// The SMB_COM_TREE_CONNECT_ANDX request curl 8.21.0 sends once the session is set up
/// (<c>smb_send_tree_connect</c>): four parameter words with no password, then
/// <c>\\host\share</c> and the service <c>?????</c> (any), each ended by a NUL.
/// </summary>
internal static class SmbTreeConnectRequest
{
    /// <summary>The most bytes curl's <c>struct smb_tree_connect</c> holds after its parameter words.</summary>
    public const int MaxByteCount = 1024;

    // Word count, AndX (4), flags (2), password length (2), byte count (2).
    private const int ParametersLength = 11;

    // SMB_WC_TREE_CONNECT_ANDX.
    private const byte WordCount = 0x04;

    private const int ByteCountOffset = 9;

    // SERVICENAME: any type of service.
    private static ReadOnlySpan<byte> ServiceName => "?????"u8;

    /// <summary>
    /// Encodes the request, NetBIOS header first, or refuses it as curl does when its
    /// bytes would pass <see cref="MaxByteCount" />.
    /// </summary>
    /// <param name="hostName">The host name the transfer connected to.</param>
    /// <param name="share">The share's name, as decoded from the URL.</param>
    /// <param name="userId">The UID the session setup response assigned.</param>
    /// <returns>The bytes to send, or <see langword="null" /> when they would not fit.</returns>
    public static byte[]? Encode(string hostName, byte[] share, ushort userId)
    {
        byte[] host = Encoding.UTF8.GetBytes(hostName);
        int byteCount = 2 + host.Length + 1 + share.Length + 1 + ServiceName.Length + 1;
        if (byteCount > MaxByteCount)
        {
            return null;
        }

        var body = new byte[ParametersLength + byteCount];
        body[0] = WordCount;
        body[1] = SmbMessageHeader.NoAndXCommand;
        BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(ByteCountOffset), (ushort)byteCount);
        Span<byte> bytes = body.AsSpan(ParametersLength);
        bytes[0] = (byte)'\\';
        bytes[1] = (byte)'\\';
        host.CopyTo(bytes[2..]);
        int offset = 2 + host.Length;
        bytes[offset] = (byte)'\\';
        share.CopyTo(bytes[(offset + 1)..]);
        ServiceName.CopyTo(bytes[(offset + 1 + share.Length + 1)..]);
        return SmbMessageHeader.Frame(SmbMessageHeader.TreeConnectAndXCommand, userId, 0, body);
    }
}
