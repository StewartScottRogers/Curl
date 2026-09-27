using System.Text;

namespace Curl.Authentication;

/// <summary>
/// Chooses the encoding that turns a user name, password or token into the bytes the
/// platform's curl sends (ADR-0022).
/// </summary>
public static class CredentialEncoding
{
    /// <summary>The code page of Windows-1252, the ANSI code page of an English Windows system.</summary>
    private const int Windows1252CodePage = 1252;

    /// <summary>
    /// Gets the encoding the platform's curl sends credentials in.
    /// </summary>
    /// <param name="isWindows">
    /// <see langword="true" /> on Windows, where the reference curl receives its arguments
    /// in the system's ANSI code page (Windows-1252 on an English system), unmappable
    /// characters best-fitted; <see langword="false" /> elsewhere, where curl sends the
    /// argument's bytes as given, which is UTF-8.
    /// </param>
    /// <returns>
    /// The system ANSI code page on Windows, or Windows-1252 when Windows behaviour is asked
    /// for on a host that has no ANSI code page; UTF-8 elsewhere.
    /// </returns>
    public static Encoding ForPlatform(bool isWindows) =>
        ForPlatformGivenSystemAnsiCodePage(isWindows, CodePagesEncodingProvider.Instance.GetEncoding(0));

    /// <summary>
    /// Gets the encoding the platform's curl sends credentials in, given the host's system
    /// ANSI code page.
    /// </summary>
    /// <param name="isWindows"><see langword="true" /> for Windows behaviour.</param>
    /// <param name="systemAnsiCodePage">
    /// The host's system ANSI code page, or <see langword="null" /> on a host that has none
    /// (Linux and macOS, where <see cref="CodePagesEncodingProvider" /> answers code page 0
    /// with <see langword="null" />).
    /// </param>
    /// <returns>
    /// <paramref name="systemAnsiCodePage" /> on Windows, Windows-1252 when that is
    /// <see langword="null" />; UTF-8 elsewhere.
    /// </returns>
    internal static Encoding ForPlatformGivenSystemAnsiCodePage(bool isWindows, Encoding? systemAnsiCodePage) =>
        isWindows
            ? systemAnsiCodePage ?? CodePagesEncodingProvider.Instance.GetEncoding(Windows1252CodePage)!
            : Encoding.UTF8;
}
