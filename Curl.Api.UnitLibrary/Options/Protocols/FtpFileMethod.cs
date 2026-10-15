namespace Curl.Api.Options.Protocols;

/// <summary>
/// How FTP reaches a file in a remote directory: libcurl's <c>CURLFTPMETHOD_*</c> values for
/// <c>CURLOPT_FTP_FILEMETHOD</c>, with the same numbers.
/// </summary>
public enum FtpFileMethod
{
    /// <summary><c>CURLFTPMETHOD_DEFAULT</c>: let libcurl decide, which is <see cref="MultiCwd"/>.</summary>
    Default = 0,

    /// <summary><c>CURLFTPMETHOD_MULTICWD</c>: one <c>CWD</c> per path part (libcurl's default).</summary>
    MultiCwd = 1,

    /// <summary><c>CURLFTPMETHOD_NOCWD</c>: no <c>CWD</c>; send the full path with each command.</summary>
    NoCwd = 2,

    /// <summary><c>CURLFTPMETHOD_SINGLECWD</c>: one <c>CWD</c> to the whole directory, then the file name.</summary>
    SingleCwd = 3,
}
