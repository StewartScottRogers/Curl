using System.Globalization;

namespace Curl.Console;

/// <summary>
/// The lines curl 8.21.0 writes to standard error for its progress meter, measured on
/// Windows on 2026-09-26 with a ten-byte <c>file://</c> source and standard error
/// redirected to a file.
/// </summary>
/// <remarks>
/// Only the meter's opening is modelled: the two header lines and the first status line,
/// whose counters are all zero. That is every byte curl writes for a <c>file://</c>
/// transfer. For a network transfer curl rewrites the status line in place with the sizes,
/// speeds and times it measures as the transfer runs, which is not modelled here.
/// </remarks>
internal static class ProgressMeterLines
{
    /// <summary>The meter's first header line.</summary>
    internal const string FirstHeaderLine =
        "  % Total    % Received % Xferd  Average Speed  Time    Time    Time   Current";

    /// <summary>The meter's second header line.</summary>
    internal const string SecondHeaderLine =
        "                                 Dload  Upload  Total   Spent   Left   Speed";

    /// <summary>
    /// The meter's first status line, every counter zero, preceded by the carriage return
    /// curl writes before each status line so the next one overwrites it.
    /// </summary>
    internal const string ZeroStatusLine =
        "\r  0      0   0      0   0      0      0      0                              0";

    /// <summary>
    /// The lines curl writes before a transfer's meter when it resumes past byte zero, as
    /// <c>-C N</c> does for N &gt; 0.
    /// </summary>
    /// <param name="resumeFrom">The byte position the transfer resumes from.</param>
    /// <returns><c>** Resuming transfer from byte position N</c>.</returns>
    internal static string ResumingLine(long resumeFrom) =>
        "** Resuming transfer from byte position " + resumeFrom.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// The meter's opening lines, in the order curl writes them, each without a terminator.
    /// </summary>
    /// <param name="resumeFrom">
    /// The resolved <c>-C</c> offset, or <see langword="null" /> without <c>-C</c>. Past zero,
    /// <see cref="ResumingLine" /> comes first.
    /// </param>
    /// <returns>The lines.</returns>
    internal static IReadOnlyList<string> Opening(long? resumeFrom) =>
        resumeFrom is > 0 and long position
            ? [ResumingLine(position), FirstHeaderLine, SecondHeaderLine, ZeroStatusLine]
            : [FirstHeaderLine, SecondHeaderLine, ZeroStatusLine];
}
