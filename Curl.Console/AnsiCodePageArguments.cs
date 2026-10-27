using System.Text;
using Curl.Authentication;

namespace Curl.Console;

/// <summary>
/// Reads the command line as the reference curl on Windows reads its ANSI <c>argv</c> (CGR-015,
/// BL-3892, ADR-0838): each argument is converted to the system ANSI code page with Windows best fit
/// and back, so a character the code page lacks becomes its best-fit stand-in (the full-width digit
/// U+FF10 becomes <c>0</c>) or <c>?</c> (<c>中</c>), while one it has (<c>é</c>, <c>“</c>) is kept.
/// Off Windows the arguments pass through unchanged.
/// </summary>
internal static class AnsiCodePageArguments
{
    /// <summary>
    /// The arguments as this platform's curl reads them: through the system ANSI code page on
    /// Windows (<see cref="CredentialEncoding.ForPlatform" />), unchanged elsewhere.
    /// </summary>
    /// <param name="arguments">The arguments as the shell passed them.</param>
    /// <param name="isWindows">Whether the process runs on Windows.</param>
    /// <returns>The arguments curl sees.</returns>
    public static IReadOnlyList<string> ForPlatform(IReadOnlyList<string> arguments, bool isWindows) =>
        isWindows ? ThroughCodePage(arguments, CredentialEncoding.ForPlatform(isWindows: true)) : arguments;

    /// <summary>
    /// Converts each argument to <paramref name="ansiCodePage" />'s bytes, which a code-page encoding
    /// does with Windows best fit, and back to text.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="ansiCodePage">The ANSI code page.</param>
    /// <returns>The arguments as the code page holds them.</returns>
    public static IReadOnlyList<string> ThroughCodePage(IReadOnlyList<string> arguments, Encoding ansiCodePage) =>
        [.. arguments.Select(argument => ansiCodePage.GetString(ansiCodePage.GetBytes(argument)))];
}
