namespace Curl.Original.Protocol.Ftp;

/// <summary>
/// The type of a file in an FTP <c>LIST</c> reply, as upstream curl 8.22.0's
/// <c>lib/ftplistparser.c</c> reads it: libcurl's <c>curlfiletype</c>, with its numbers.
/// </summary>
public enum FtpFileType
{
    /// <summary><c>CURLFILETYPE_FILE</c> (0): a regular file, <c>-</c> in a Unix listing.</summary>
    File = 0,

    /// <summary><c>CURLFILETYPE_DIRECTORY</c> (1): a directory, <c>d</c> or <c>&lt;DIR&gt;</c>.</summary>
    Directory = 1,

    /// <summary><c>CURLFILETYPE_SYMLINK</c> (2): a symbolic link, <c>l</c>.</summary>
    SymbolicLink = 2,

    /// <summary><c>CURLFILETYPE_DEVICE_BLOCK</c> (3): a block device, <c>b</c>.</summary>
    DeviceBlock = 3,

    /// <summary><c>CURLFILETYPE_DEVICE_CHAR</c> (4): a character device, <c>c</c>.</summary>
    DeviceChar = 4,

    /// <summary><c>CURLFILETYPE_NAMEDPIPE</c> (5): a named pipe, <c>p</c>.</summary>
    NamedPipe = 5,

    /// <summary><c>CURLFILETYPE_SOCKET</c> (6): a socket, <c>s</c>.</summary>
    Socket = 6,

    /// <summary><c>CURLFILETYPE_DOOR</c> (7): a Solaris door, <c>D</c>.</summary>
    Door = 7,
}
