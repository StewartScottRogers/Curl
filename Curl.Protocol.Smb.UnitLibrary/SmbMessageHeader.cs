using System.Buffers.Binary;

namespace Curl.Protocol.Smb;

/// <summary>
/// The 4-byte NetBIOS session header and the 32-byte SMBv1 header every message starts
/// with, written and read as curl 8.21.0's <c>struct smb_header</c> lays them out
/// (<c>lib/smb.c</c>, <c>smb_format_message</c>): little-endian fields, no signing.
/// </summary>
internal static class SmbMessageHeader
{
    /// <summary>The NetBIOS session header and SMB header together, 36 bytes.</summary>
    public const int Length = 36;

    /// <summary>The length of the NetBIOS session header that frames each message.</summary>
    public const int NetBiosHeaderLength = 4;

    /// <summary>SMB_COM_NEGOTIATE.</summary>
    public const byte NegotiateCommand = 0x72;

    /// <summary>SMB_COM_SESSION_SETUP_ANDX.</summary>
    public const byte SessionSetupAndXCommand = 0x73;

    /// <summary>SMB_COM_TREE_CONNECT_ANDX.</summary>
    public const byte TreeConnectAndXCommand = 0x75;

    /// <summary>SMB_COM_NT_CREATE_ANDX, which opens the file.</summary>
    public const byte NtCreateAndXCommand = 0xa2;

    /// <summary>SMB_COM_READ_ANDX.</summary>
    public const byte ReadAndXCommand = 0x2e;

    /// <summary>SMB_COM_CLOSE.</summary>
    public const byte CloseCommand = 0x04;

    /// <summary>SMB_COM_TREE_DISCONNECT.</summary>
    public const byte TreeDisconnectCommand = 0x71;

    /// <summary>SMB_COM_NO_ANDX_COMMAND, the AndX command of every request curl sends.</summary>
    public const byte NoAndXCommand = 0xff;

    private const int CommandOffset = 8;
    private const int StatusOffset = 9;
    private const int FlagsOffset = 13;
    private const int Flags2Offset = 14;
    private const int ProcessIdHighOffset = 16;
    private const int TreeIdOffset = 28;
    private const int ProcessIdOffset = 30;
    private const int UserIdOffset = 32;

    // SMB_FLAGS_CANONICAL_PATHNAMES | SMB_FLAGS_CASELESS_PATHNAMES.
    private const byte Flags = 0x18;

    // SMB_FLAGS2_IS_LONG_NAME | SMB_FLAGS2_KNOWS_LONG_NAME.
    private const ushort Flags2 = 0x0041;

    // curl's made-up process ID, "0xbad71d", split into its high and low 16 bits.
    private const uint ProcessId = 0xbad71d;

    private static ReadOnlySpan<byte> Magic => [0xff, (byte)'S', (byte)'M', (byte)'B'];

    /// <summary>
    /// Writes the header of a message of <paramref name="command" /> whose parameters and
    /// data, after the header, are <paramref name="bodyLength" /> bytes.
    /// </summary>
    /// <param name="destination">At least <see cref="Length" /> bytes; the first <see cref="Length" /> are overwritten.</param>
    /// <param name="command">The SMB command code.</param>
    /// <param name="userId">The UID the session setup response assigned, or 0 before it.</param>
    /// <param name="treeId">The TID the tree connect response assigned, or 0 before it.</param>
    /// <param name="bodyLength">The length of the word count, parameters, byte count and bytes that follow.</param>
    public static void Write(Span<byte> destination, byte command, ushort userId, ushort treeId, int bodyLength)
    {
        Span<byte> header = destination[..Length];
        header.Clear();
        BinaryPrimitives.WriteUInt16BigEndian(header[2..], (ushort)(Length - NetBiosHeaderLength + bodyLength));
        Magic.CopyTo(header[NetBiosHeaderLength..]);
        header[CommandOffset] = command;
        header[FlagsOffset] = Flags;
        BinaryPrimitives.WriteUInt16LittleEndian(header[Flags2Offset..], Flags2);
        BinaryPrimitives.WriteUInt16LittleEndian(header[ProcessIdHighOffset..], (ushort)(ProcessId >> 16));
        BinaryPrimitives.WriteUInt16LittleEndian(header[TreeIdOffset..], treeId);
        BinaryPrimitives.WriteUInt16LittleEndian(header[ProcessIdOffset..], (ushort)(ProcessId & 0xffff));
        BinaryPrimitives.WriteUInt16LittleEndian(header[UserIdOffset..], userId);
    }

    /// <summary>
    /// Frames a request: the header of a message of <paramref name="command" />, then
    /// <paramref name="body" />.
    /// </summary>
    /// <param name="command">The SMB command code.</param>
    /// <param name="userId">The UID the session setup response assigned.</param>
    /// <param name="treeId">The TID the tree connect response assigned, or 0 before it.</param>
    /// <param name="body">The word count, parameters, byte count and bytes.</param>
    /// <returns>The bytes to send, NetBIOS header first.</returns>
    public static byte[] Frame(byte command, ushort userId, ushort treeId, ReadOnlySpan<byte> body)
    {
        var message = new byte[Length + body.Length];
        Write(message, command, userId, treeId, body.Length);
        body.CopyTo(message.AsSpan(Length));
        return message;
    }

    /// <summary>Reads the 32-bit NT status of a received message; 0 is success.</summary>
    /// <param name="message">A whole message, NetBIOS header first.</param>
    /// <returns>The status, as the little-endian value on the wire.</returns>
    public static uint ReadStatus(ReadOnlySpan<byte> message) =>
        BinaryPrimitives.ReadUInt32LittleEndian(message[StatusOffset..]);

    /// <summary>Reads the TID of a received message.</summary>
    /// <param name="message">A whole message, NetBIOS header first.</param>
    /// <returns>The TID.</returns>
    public static ushort ReadTreeId(ReadOnlySpan<byte> message) =>
        BinaryPrimitives.ReadUInt16LittleEndian(message[TreeIdOffset..]);

    /// <summary>Reads the UID of a received message.</summary>
    /// <param name="message">A whole message, NetBIOS header first.</param>
    /// <returns>The UID.</returns>
    public static ushort ReadUserId(ReadOnlySpan<byte> message) =>
        BinaryPrimitives.ReadUInt16LittleEndian(message[UserIdOffset..]);
}
