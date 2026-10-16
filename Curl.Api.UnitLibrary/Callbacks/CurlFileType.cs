namespace Curl.Api.Callbacks;

/// <summary>The type of a file in a wildcard listing: libcurl's <c>curlfiletype</c> (<c>CURLFILETYPE_*</c>), with upstream curl 8.22.0's numbers.</summary>
public enum CurlFileType
{
    /// <summary><c>CURLFILETYPE_FILE</c> (0): a regular file.</summary>
    File = 0,

    /// <summary><c>CURLFILETYPE_DIRECTORY</c> (1): a directory.</summary>
    Directory = 1,

    /// <summary><c>CURLFILETYPE_SYMLINK</c> (2): a symbolic link.</summary>
    SymbolicLink = 2,

    /// <summary><c>CURLFILETYPE_DEVICE_BLOCK</c> (3): a block device.</summary>
    DeviceBlock = 3,

    /// <summary><c>CURLFILETYPE_DEVICE_CHAR</c> (4): a character device.</summary>
    DeviceChar = 4,

    /// <summary><c>CURLFILETYPE_NAMEDPIPE</c> (5): a named pipe.</summary>
    NamedPipe = 5,

    /// <summary><c>CURLFILETYPE_SOCKET</c> (6): a socket.</summary>
    Socket = 6,

    /// <summary><c>CURLFILETYPE_DOOR</c> (7): a Solaris door.</summary>
    Door = 7,

    /// <summary><c>CURLFILETYPE_UNKNOWN</c> (8): a type libcurl could not tell.</summary>
    Unknown = 8,
}
