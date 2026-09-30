using Curl.Protocol.Ssh.Transport;

namespace Curl.Protocol.Ssh.Sftp;

/// <summary>
/// A file's attributes as libssh2 1.11.1 holds them (draft-ietf-secsh-filexfer-02 section
/// 5): the flags that say which fields are present, then the size, the owner and group,
/// the permissions and the access and modification times. The extended attributes are
/// read past and never kept, as libssh2 does.
/// </summary>
/// <param name="Flags">The <c>SSH_FILEXFER_ATTR_*</c> bits that say which fields are present.</param>
/// <param name="Size">The size, when <see cref="SizeFlag" /> is set.</param>
/// <param name="UserId">The owner's number, when <see cref="UserAndGroupFlag" /> is set.</param>
/// <param name="GroupId">The group's number, when <see cref="UserAndGroupFlag" /> is set.</param>
/// <param name="Permissions">The permission and file-type bits, when <see cref="PermissionsFlag" /> is set.</param>
/// <param name="AccessTime">The access time in seconds since the epoch, when <see cref="AccessAndModifyTimesFlag" /> is set.</param>
/// <param name="ModifyTime">The modification time in seconds since the epoch, when <see cref="AccessAndModifyTimesFlag" /> is set.</param>
internal readonly record struct SftpAttributes(uint Flags, ulong Size, uint UserId, uint GroupId, uint Permissions, uint AccessTime, uint ModifyTime)
{
    /// <summary><c>SSH_FILEXFER_ATTR_SIZE</c>.</summary>
    internal const uint SizeFlag = 0x00000001;

    /// <summary><c>SSH_FILEXFER_ATTR_UIDGID</c>.</summary>
    internal const uint UserAndGroupFlag = 0x00000002;

    /// <summary><c>SSH_FILEXFER_ATTR_PERMISSIONS</c>.</summary>
    internal const uint PermissionsFlag = 0x00000004;

    /// <summary><c>SSH_FILEXFER_ATTR_ACMODTIME</c>.</summary>
    internal const uint AccessAndModifyTimesFlag = 0x00000008;

    /// <summary><c>SSH_FILEXFER_ATTR_EXTENDED</c>.</summary>
    internal const uint ExtendedFlag = 0x80000000;

    // The flags libssh2's sftp_attr2bin writes; any other bit is dropped.
    private const uint WrittenFlags = SizeFlag | UserAndGroupFlag | PermissionsFlag | AccessAndModifyTimesFlag;

    /// <summary>
    /// Reads attributes from <paramref name="reader" />, past any extended attributes.
    /// </summary>
    /// <param name="reader">A reader at the attributes' flags.</param>
    /// <returns>The attributes; a field whose flag is clear is 0.</returns>
    /// <exception cref="InvalidDataException">The attributes are cut short.</exception>
    internal static SftpAttributes Read(SshWireReader reader)
    {
        uint flags = reader.ReadUInt32();
        (uint sizeHigh, uint sizeLow) = ReadPairWhen(reader, flags, SizeFlag);
        (uint userId, uint groupId) = ReadPairWhen(reader, flags, UserAndGroupFlag);
        uint permissions = Has(flags, PermissionsFlag) ? reader.ReadUInt32() : 0;
        (uint accessTime, uint modifyTime) = ReadPairWhen(reader, flags, AccessAndModifyTimesFlag);
        SkipExtended(reader, flags);
        return new SftpAttributes(flags, ((ulong)sizeHigh << 32) | sizeLow, userId, groupId, permissions, accessTime, modifyTime);
    }

    /// <summary>
    /// Writes the attributes as libssh2's <c>sftp_attr2bin</c> does: the flags, less any
    /// but the size, owner and group, permissions and times, then each field they name.
    /// </summary>
    /// <param name="writer">Where the attributes go.</param>
    internal void WriteTo(SshWireWriter writer)
    {
        writer.WriteUInt32(Flags & WrittenFlags);
        if (Has(Flags, SizeFlag))
        {
            writer.WriteUInt32((uint)(Size >> 32));
            writer.WriteUInt32((uint)Size);
        }

        WritePairWhen(writer, UserAndGroupFlag, UserId, GroupId);
        if (Has(Flags, PermissionsFlag))
        {
            writer.WriteUInt32(Permissions);
        }

        WritePairWhen(writer, AccessAndModifyTimesFlag, AccessTime, ModifyTime);
    }

    private static bool Has(uint flags, uint flag) => (flags & flag) != 0;

    // Two 32-bit fields when the flag is set; the size is its high and low halves.
    private static (uint First, uint Second) ReadPairWhen(SshWireReader reader, uint flags, uint flag) =>
        Has(flags, flag) ? (reader.ReadUInt32(), reader.ReadUInt32()) : (0u, 0u);

    private static void SkipExtended(SshWireReader reader, uint flags)
    {
        uint count = Has(flags, ExtendedFlag) ? reader.ReadUInt32() : 0;
        for (uint index = 0; index < count; index++)
        {
            reader.ReadString();
            reader.ReadString();
        }
    }

    private void WritePairWhen(SshWireWriter writer, uint flag, uint first, uint second)
    {
        if (Has(Flags, flag))
        {
            writer.WriteUInt32(first);
            writer.WriteUInt32(second);
        }
    }
}
