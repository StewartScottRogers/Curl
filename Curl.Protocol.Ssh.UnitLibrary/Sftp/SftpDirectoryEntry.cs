namespace Curl.Protocol.Ssh.Sftp;

/// <summary>
/// One name of an <c>SSH_FXP_NAME</c> answer to <c>SSH_FXP_READDIR</c>, as curl 8.21.0 uses
/// it to list a directory (ADR-0241).
/// </summary>
/// <param name="FileName">The entry's file name, as the server sent its bytes.</param>
/// <param name="LongName">The server's <c>ls -l</c>-style line for the entry, as its bytes; empty when the server sent none.</param>
/// <param name="IsSymbolicLink">
/// Whether the attributes carry permissions whose file type is a symbolic link
/// (<c>S_IFLNK</c>), which curl follows with <c>SSH_FXP_READLINK</c>.
/// </param>
internal sealed record SftpDirectoryEntry(byte[] FileName, byte[] LongName, bool IsSymbolicLink);
