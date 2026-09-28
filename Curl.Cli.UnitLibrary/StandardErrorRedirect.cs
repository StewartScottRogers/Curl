namespace Curl.Cli;

/// <summary>
/// One <c>--stderr</c> option, at the point in the command line where curl 8.21.0 opens its file: the
/// warning lines met before it stay where standard error went until then, and the lines after it go to
/// <see cref="File"/>, until a later <c>--stderr</c> moves them again. Nothing here is opened.
/// </summary>
/// <remarks>
/// Measured on curl 8.21.0 on 2026-09-27 (BL-410, BL-476): <c>--stderr se -H nocolon</c> writes the
/// <c>-H</c> warning to <c>se</c> and <c>-H nocolon --stderr se</c> to standard error;
/// <c>--stderr a -H x --stderr b</c> writes the warning to <c>a</c> and the rest to <c>b</c>;
/// <c>--stderr adir -s</c> prints <c>Warning: Warning: Failed to open adir</c> and
/// <c>-s --stderr adir</c> prints nothing.
/// </remarks>
/// <param name="File">The file the option names: <c>-</c> for standard output; possibly empty, which curl fails to open.</param>
/// <param name="WarningLinesBefore">
/// How many of <see cref="CommandLineParseResult.WarningLines"/> were met before this option, its own
/// file-name-looks-like-a-flag warning included.
/// </param>
/// <param name="Silent">
/// Whether <c>-s</c> / <c>--silent</c> was in effect when the option was read, which hides the warning
/// for a file that cannot be opened.
/// </param>
public sealed record StandardErrorRedirect(string File, int WarningLinesBefore, bool Silent);
