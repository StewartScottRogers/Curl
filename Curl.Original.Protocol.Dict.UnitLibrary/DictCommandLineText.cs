using System.Text;

namespace Curl.Original.Protocol.Dict;

/// <summary>
/// Turns text taken from the command line - a URL path written with literal non-ASCII characters, read for the dict word - into the bytes the platform's curl sends,
/// which are the bytes curl received in its argv (BL-3842, ADR-0907): the system ANSI code
/// page on Windows, where a character the code page lacks is best-fitted (measured with
/// <c>Record-CurlExchange.ps1</c>: <c>é</c> went out as <c>E9</c>), and UTF-8 on Linux and macOS.
/// </summary>
/// <param name="encoding">The encoding the platform's curl receives its arguments in.</param>
internal sealed class DictCommandLineText(Encoding encoding)
{
    /// <summary>The code page of Windows-1252, the ANSI code page of an English Windows system.</summary>
    private const int Windows1252CodePage = 1252;

    /// <summary>
    /// Gets the command-line text of the host this runs on: the system ANSI code page on
    /// Windows, UTF-8 elsewhere.
    /// </summary>
    public static DictCommandLineText Platform { get; } =
        ForPlatform(OperatingSystem.IsWindows(), ReadSystemAnsiCodePage(() => CodePagesEncodingProvider.Instance.GetEncoding(0)));

    /// <summary>
    /// Reads the host's system ANSI code page, asking a second time when the first answer is
    /// <see langword="null" />, because Windows can fail one of several first calls made at
    /// once (BL-1200, BL-1203). On Linux and macOS both answers are <see langword="null" />.
    /// </summary>
    /// <param name="readCodePageZero">Asks the code page provider for code page 0.</param>
    /// <returns>The system ANSI code page, or <see langword="null" /> when the host has none.</returns>
    internal static Encoding? ReadSystemAnsiCodePage(Func<Encoding?> readCodePageZero) =>
        readCodePageZero() ?? readCodePageZero();

    /// <summary>
    /// Chooses the command-line text of a platform, given the host's system ANSI code page.
    /// </summary>
    /// <param name="isWindows"><see langword="true" /> for Windows behaviour.</param>
    /// <param name="systemAnsiCodePage">
    /// The host's system ANSI code page, or <see langword="null" /> on a host that has none.
    /// </param>
    /// <returns>
    /// <paramref name="systemAnsiCodePage" /> on Windows, Windows-1252 when that is
    /// <see langword="null" />; UTF-8 elsewhere.
    /// </returns>
    public static DictCommandLineText ForPlatform(bool isWindows, Encoding? systemAnsiCodePage) =>
        new(isWindows
            ? systemAnsiCodePage ?? CodePagesEncodingProvider.Instance.GetEncoding(Windows1252CodePage)!
            : Encoding.UTF8);

    /// <summary>
    /// Gets <paramref name="text" /> as the bytes curl received in its argv.
    /// </summary>
    /// <param name="text">The text as given on the command line.</param>
    /// <returns>The text's bytes in the argv encoding.</returns>
    public byte[] GetBytes(string text) => encoding.GetBytes(text);
}
