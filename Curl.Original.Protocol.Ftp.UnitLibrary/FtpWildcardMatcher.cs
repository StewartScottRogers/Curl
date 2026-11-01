namespace Curl.Original.Protocol.Ftp;

/// <summary>
/// Which <c>Curl_fnmatch</c> an FTP wildcard pattern is matched as: curl's own matcher, which the
/// Windows build uses, or the system <c>fnmatch(3)</c> the Linux and macOS builds call instead
/// (<c>HAVE_FNMATCH</c>), whose answers upstream's <c>tests/unit/unit1307.c</c> marks <c>LINUX_*</c>
/// and <c>MAC_*</c> where they differ (BL-2480).
/// </summary>
public enum FtpWildcardMatcher
{
    /// <summary>curl's own <c>Curl_fnmatch</c> (<c>lib/curl_fnmatch.c</c>), as the Windows build matches.</summary>
    Curl,

    /// <summary>glibc's <c>fnmatch</c> in a UTF-8 locale, as the Linux build matches.</summary>
    Linux,

    /// <summary>macOS libc's <c>fnmatch</c> in a UTF-8 locale, as the macOS build matches.</summary>
    MacOs,
}
