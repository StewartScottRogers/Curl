namespace Curl.Protocol.Smb;

/// <summary>
/// The SMB_COM_NEGOTIATE request curl 8.21.0 opens every session with
/// (<c>smb_send_negotiate</c>): no parameter words and one dialect, <c>NT LM 0.12</c>.
/// </summary>
internal static class SmbNegotiateRequest
{
    // Word count 0, byte count 12, then buffer format 0x02 and "NT LM 0.12" with its NUL.
    private static ReadOnlySpan<byte> Body =>
        [0x00, 0x0c, 0x00, 0x02, (byte)'N', (byte)'T', (byte)' ', (byte)'L', (byte)'M', (byte)' ', (byte)'0', (byte)'.', (byte)'1', (byte)'2', 0x00];

    /// <summary>Encodes the request, NetBIOS header first: 51 bytes.</summary>
    /// <returns>The bytes to send.</returns>
    public static byte[] Encode()
    {
        var message = new byte[SmbMessageHeader.Length + Body.Length];
        SmbMessageHeader.Write(message, SmbMessageHeader.NegotiateCommand, 0, 0, Body.Length);
        Body.CopyTo(message.AsSpan(SmbMessageHeader.Length));
        return message;
    }
}
