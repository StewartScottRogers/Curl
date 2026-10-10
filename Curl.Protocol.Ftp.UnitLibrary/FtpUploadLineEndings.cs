namespace Curl.Protocol.Ftp;

/// <summary>
/// Decides whether an FTP upload's line feeds are converted to carriage-return line-feed
/// pairs before they are sent. Public so its tests reach the off-Windows answer on Windows.
/// </summary>
/// <remarks>
/// curl adds its line-conversion reader under <c>--crlf</c> on every platform, and for an
/// ASCII transfer (<c>-B</c> or a <c>;type=a</c> URL suffix) only where it is built with
/// <c>CURL_PREFER_LF_LINEENDS</c>, which is everywhere but Windows: upstream's test475 uploads
/// a file of LF lines on Linux and macOS and a file of CRLF lines on Windows, and expects CRLF
/// lines on the wire from both (BL-1957).
/// </remarks>
public static class FtpUploadLineEndings
{
    /// <summary>Whether the upload's line feeds are converted.</summary>
    /// <param name="convertLineEndings">Whether <c>--crlf</c> was given.</param>
    /// <param name="useAscii">Whether the transfer is in ASCII mode.</param>
    /// <param name="runsOnWindows">Whether Curl runs on Windows.</param>
    /// <returns><see langword="true" /> under <c>--crlf</c>, or for ASCII off Windows.</returns>
    public static bool AreConverted(bool convertLineEndings, bool useAscii, bool runsOnWindows) =>
        convertLineEndings || (useAscii && !runsOnWindows);
}
