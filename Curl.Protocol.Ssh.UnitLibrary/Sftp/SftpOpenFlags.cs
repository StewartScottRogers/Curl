namespace Curl.Protocol.Ssh.Sftp;

/// <summary>
/// The <c>SSH_FXF_*</c> bits of an <c>SSH_FXP_OPEN</c> (draft-ietf-secsh-filexfer-02
/// section 6.3) that curl 8.21.0 sends through libssh2 1.11.1.
/// </summary>
internal static class SftpOpenFlags
{
    /// <summary><c>SSH_FXF_READ</c>: open for reading, as every download does.</summary>
    internal const uint Read = 0x01;

    /// <summary><c>SSH_FXF_WRITE</c>: open for writing, alone for a <c>-C</c> upload (measured, ADR-0244).</summary>
    internal const uint Write = 0x02;

    /// <summary><c>SSH_FXF_APPEND</c>: every write goes to the end of the file, as <c>-a</c> asks.</summary>
    internal const uint Append = 0x04;

    /// <summary><c>SSH_FXF_CREAT</c>: create the file when it does not exist.</summary>
    internal const uint Create = 0x08;

    /// <summary><c>SSH_FXF_TRUNC</c>: empty an existing file first.</summary>
    internal const uint Truncate = 0x10;
}
