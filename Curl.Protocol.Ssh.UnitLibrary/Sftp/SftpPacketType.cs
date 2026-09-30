namespace Curl.Protocol.Ssh.Sftp;

/// <summary>
/// The SFTP version 3 packet types (draft-ietf-secsh-filexfer-02 section 3) that this
/// library reads or writes.
/// </summary>
internal static class SftpPacketType
{
    /// <summary><c>SSH_FXP_INIT</c>: the client's first packet, carrying the version it speaks.</summary>
    internal const byte Init = 1;

    /// <summary><c>SSH_FXP_VERSION</c>: the server's answer to <see cref="Init" />, with its version and extensions.</summary>
    internal const byte Version = 2;

    /// <summary><c>SSH_FXP_OPEN</c>: opens a file, answered by a handle or a status.</summary>
    internal const byte Open = 3;

    /// <summary><c>SSH_FXP_CLOSE</c>: closes a handle.</summary>
    internal const byte Close = 4;

    /// <summary><c>SSH_FXP_READ</c>: asks for up to a length of bytes at an offset of an open file.</summary>
    internal const byte Read = 5;

    /// <summary><c>SSH_FXP_WRITE</c>: writes bytes at an offset of an open file, answered by a status.</summary>
    internal const byte Write = 6;

    /// <summary><c>SSH_FXP_SETSTAT</c>: sets a path's attributes, answered by a status.</summary>
    internal const byte SetStat = 9;

    /// <summary><c>SSH_FXP_OPENDIR</c>: opens a directory for listing, answered by a handle or a status.</summary>
    internal const byte OpenDirectory = 11;

    /// <summary><c>SSH_FXP_READDIR</c>: asks for the next names of an open directory, answered by names or a status.</summary>
    internal const byte ReadDirectory = 12;

    /// <summary><c>SSH_FXP_REMOVE</c>: removes a file, answered by a status.</summary>
    internal const byte Remove = 13;

    /// <summary><c>SSH_FXP_MKDIR</c>: creates a directory with attributes, answered by a status.</summary>
    internal const byte MakeDirectory = 14;

    /// <summary><c>SSH_FXP_RMDIR</c>: removes a directory, answered by a status.</summary>
    internal const byte RemoveDirectory = 15;

    /// <summary><c>SSH_FXP_REALPATH</c>: asks the server to make a path absolute; curl sends <c>.</c> for the home directory.</summary>
    internal const byte RealPath = 16;

    /// <summary><c>SSH_FXP_STAT</c>: asks for a path's attributes, following links.</summary>
    internal const byte Stat = 17;

    /// <summary><c>SSH_FXP_RENAME</c>: renames a path, answered by a status.</summary>
    internal const byte Rename = 18;

    /// <summary><c>SSH_FXP_READLINK</c>: asks for a symbolic link's target, answered by a name or a status.</summary>
    internal const byte ReadLink = 19;

    /// <summary><c>SSH_FXP_SYMLINK</c>: creates a symbolic link, answered by a status.</summary>
    internal const byte SymbolicLink = 20;

    /// <summary><c>SSH_FXP_EXTENDED</c>: a named extension's request, such as <c>statvfs@openssh.com</c>.</summary>
    internal const byte Extended = 200;

    /// <summary><c>SSH_FXP_EXTENDED_REPLY</c>: an extension's answer.</summary>
    internal const byte ExtendedReply = 201;

    /// <summary><c>SSH_FXP_STATUS</c>: a request's outcome, one of <see cref="SftpStatusCode" />.</summary>
    internal const byte Status = 101;

    /// <summary><c>SSH_FXP_HANDLE</c>: the handle of an opened file.</summary>
    internal const byte Handle = 102;

    /// <summary><c>SSH_FXP_DATA</c>: the bytes a read returned.</summary>
    internal const byte Data = 103;

    /// <summary><c>SSH_FXP_NAME</c>: names, such as the answer to <see cref="RealPath" />, <see cref="ReadDirectory" /> or <see cref="ReadLink" />.</summary>
    internal const byte Name = 104;

    /// <summary><c>SSH_FXP_ATTRS</c>: attributes, such as the answer to <see cref="Stat" />.</summary>
    internal const byte Attributes = 105;
}
