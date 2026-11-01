namespace Curl.Original.Protocol.Ftp.Fakes;

/// <summary>
/// What an ASCII FTP download (a listing, <c>-B</c> or <c>;type=a</c>) writes of the bytes a
/// server sends: those bytes on Windows, and every CRLF and lone CR as LF elsewhere, as curl
/// does where it is built with <c>CURL_PREFER_LF_LINEENDS</c> (CGR-024, BL-3553).
/// </summary>
internal static class FtpAsciiOutput
{
    public static string Of(string served) =>
        OperatingSystem.IsWindows() ? served : served.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
}
