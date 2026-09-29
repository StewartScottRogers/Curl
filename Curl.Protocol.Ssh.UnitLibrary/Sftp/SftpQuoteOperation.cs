namespace Curl.Protocol.Ssh.Sftp;

/// <summary>
/// What an SFTP <c>-Q</c> command does, one per name curl 8.21.0 knows (ADR-0247).
/// </summary>
internal enum SftpQuoteOperation
{
    /// <summary>No command: a name curl does not know.</summary>
    None = 0,

    /// <summary><c>pwd</c>: writes <c>257 "&lt;path&gt;" is current directory.</c> as a header line.</summary>
    PrintWorkingDirectory,

    /// <summary><c>chgrp &lt;gid&gt; &lt;path&gt;</c>: <c>STAT</c>, then <c>SETSTAT</c> of the owner and the new group.</summary>
    ChangeGroup,

    /// <summary><c>chmod &lt;octal mode&gt; &lt;path&gt;</c>: <c>SETSTAT</c> of the permissions alone.</summary>
    ChangeMode,

    /// <summary><c>chown &lt;uid&gt; &lt;path&gt;</c>: <c>STAT</c>, then <c>SETSTAT</c> of the new owner and the group.</summary>
    ChangeOwner,

    /// <summary><c>atime &lt;date&gt; &lt;path&gt;</c>: <c>STAT</c>, then <c>SETSTAT</c> of the new access time and the modification time.</summary>
    SetAccessTime,

    /// <summary><c>mtime &lt;date&gt; &lt;path&gt;</c>: <c>STAT</c>, then <c>SETSTAT</c> of the access time and the new modification time.</summary>
    SetModifyTime,

    /// <summary><c>ln</c> or <c>symlink &lt;path&gt; &lt;link&gt;</c>: <c>SYMLINK</c> with the two paths in that order.</summary>
    SymbolicLink,

    /// <summary><c>mkdir &lt;path&gt;</c>: <c>MKDIR</c> with mode 0755.</summary>
    MakeDirectory,

    /// <summary><c>rename &lt;path&gt; &lt;new path&gt;</c>: <c>RENAME</c>.</summary>
    Rename,

    /// <summary><c>rmdir &lt;path&gt;</c>: <c>RMDIR</c>.</summary>
    RemoveDirectory,

    /// <summary><c>rm &lt;path&gt;</c>: <c>REMOVE</c>.</summary>
    Remove,

    /// <summary><c>statvfs &lt;path&gt;</c>: OpenSSH's <c>statvfs@openssh.com</c>, written as header lines.</summary>
    StatFileSystem,
}
