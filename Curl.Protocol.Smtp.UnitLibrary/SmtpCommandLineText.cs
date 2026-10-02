using System.Text;

namespace Curl.Protocol.Smtp;

/// <summary>
/// Turns text taken from the command line - a <c>--mail-from</c>, <c>--mail-rcpt</c> or
/// <c>--mail-auth</c> address - into the bytes the platform's curl sends, which are the bytes
/// curl received in its argv (BL-776, ADR-0135 §6): the system ANSI code page on Windows, where
/// a character the code page lacks is best-fitted (measured: <c>€</c> went out as <c>80</c>
/// and <c>ł</c> as <c>l</c>), and UTF-8 on Linux and macOS.
/// </summary>
/// <param name="encoding">The encoding the platform's curl receives its arguments in.</param>
internal sealed class SmtpCommandLineText(Encoding encoding)
{
    /// <summary>The code page of Windows-1252, the ANSI code page of an English Windows system.</summary>
    private const int Windows1252CodePage = 1252;

    /// <summary>
    /// Gets the command-line text of the host this runs on: the system ANSI code page on
    /// Windows, UTF-8 elsewhere.
    /// </summary>
    public static SmtpCommandLineText Platform { get; } =
        ForPlatform(OperatingSystem.IsWindows(), ReadSystemAnsiCodePage(() => CodePagesEncodingProvider.Instance.GetEncoding(0)));

    /// <summary>
    /// Reads the host's system ANSI code page, asking a second time when the first answer is
    /// <see langword="null" />.
    /// </summary>
    /// <remarks>
    /// <see cref="CodePagesEncodingProvider" /> answers code page 0 by calling Windows'
    /// <c>GetCPInfoExW(CP_ACP)</c> afresh each time, and when several threads make their first
    /// call at once, Windows fails one of them with no error code, so the provider answers
    /// <see langword="null" /> as if the host had no ANSI code page. The second call succeeds
    /// (BL-1200, BL-1203). On Linux and macOS both answers are <see langword="null" />.
    /// </remarks>
    /// <param name="readCodePageZero">Asks the code page provider for code page 0.</param>
    /// <returns>The system ANSI code page, or <see langword="null" /> when the host has none.</returns>
    internal static Encoding? ReadSystemAnsiCodePage(Func<Encoding?> readCodePageZero) =>
        readCodePageZero() ?? readCodePageZero();

    /// <summary>
    /// Chooses the command-line text of a platform, given the host's system ANSI code page.
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
    public static SmtpCommandLineText ForPlatform(bool isWindows, Encoding? systemAnsiCodePage) =>
        new(isWindows
            ? systemAnsiCodePage ?? CodePagesEncodingProvider.Instance.GetEncoding(Windows1252CodePage)!
            : Encoding.UTF8);

    /// <summary>
    /// Gets <paramref name="text" /> as curl received it: its characters after a round trip
    /// through the argv encoding, so a best-fitted <c>ł</c> is <c>l</c>.
    /// </summary>
    /// <param name="text">The text as given on the command line.</param>
    /// <returns>The text curl sees.</returns>
    public string AsReceived(string text) => encoding.GetString(encoding.GetBytes(text));

    /// <summary>
    /// Gets <paramref name="text" /> as its argv bytes, one character per byte, ready for the
    /// Latin-1 control channel.
    /// </summary>
    /// <param name="text">The text to send.</param>
    /// <returns>One character per byte curl sends.</returns>
    public string ToWire(string text) => Encoding.Latin1.GetString(encoding.GetBytes(text));
}
