using System.Buffers.Binary;

namespace Curl.Protocol.Smb;

/// <summary>
/// The SMB_COM_NT_CREATE_ANDX request curl 8.21.0 sends to open the file for a download
/// (<c>smb_send_open</c>): twenty-four parameter words asking for <c>GENERIC_READ</c>,
/// every share mode and <c>FILE_OPEN</c>, then the file's path within the share and a NUL.
/// </summary>
internal static class SmbOpenRequest
{
    /// <summary>The most bytes curl's <c>struct smb_nt_create</c> holds after its parameter words.</summary>
    public const int MaxByteCount = 1024;

    // Word count, AndX (4), reserved, name length (2), flags (4), root FID (4), access (4),
    // allocation size (8), attributes (4), share access (4), disposition (4), create options
    // (4), impersonation (4), security flags, byte count (2).
    private const int ParametersLength = 51;

    // SMB_WC_NT_CREATE_ANDX.
    private const byte WordCount = 0x18;

    private const int NameLengthOffset = 6;
    private const int AccessOffset = 16;
    private const int ShareAccessOffset = 32;
    private const int CreateDispositionOffset = 36;
    private const int ByteCountOffset = 49;

    // SMB_GENERIC_READ.
    private const uint GenericRead = 0x80000000;

    // SMB_FILE_SHARE_ALL.
    private const uint ShareAll = 0x07;

    // SMB_FILE_OPEN.
    private const uint FileOpen = 0x01;

    /// <summary>
    /// Encodes the request, NetBIOS header first, or refuses it as curl does when its
    /// bytes would pass <see cref="MaxByteCount" />.
    /// </summary>
    /// <param name="filePath">The file's path within the share, <c>\</c>-separated.</param>
    /// <param name="userId">The UID the session setup response assigned.</param>
    /// <param name="treeId">The TID the tree connect response assigned.</param>
    /// <returns>The bytes to send, or <see langword="null" /> when they would not fit.</returns>
    public static byte[]? Encode(byte[] filePath, ushort userId, ushort treeId)
    {
        int byteCount = filePath.Length + 1;
        if (byteCount > MaxByteCount)
        {
            return null;
        }

        var body = new byte[ParametersLength + byteCount];
        Span<byte> parameters = body.AsSpan(0, ParametersLength);
        parameters[0] = WordCount;
        parameters[1] = SmbMessageHeader.NoAndXCommand;
        BinaryPrimitives.WriteUInt16LittleEndian(parameters[NameLengthOffset..], (ushort)filePath.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(parameters[AccessOffset..], GenericRead);
        BinaryPrimitives.WriteUInt32LittleEndian(parameters[ShareAccessOffset..], ShareAll);
        BinaryPrimitives.WriteUInt32LittleEndian(parameters[CreateDispositionOffset..], FileOpen);
        BinaryPrimitives.WriteUInt16LittleEndian(parameters[ByteCountOffset..], (ushort)byteCount);
        filePath.CopyTo(body.AsSpan(ParametersLength));
        return SmbMessageHeader.Frame(SmbMessageHeader.NtCreateAndXCommand, userId, treeId, body);
    }
}
