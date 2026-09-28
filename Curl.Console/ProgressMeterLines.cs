using System.Globalization;

namespace Curl.Console;

/// <summary>
/// The lines curl 8.21.0 writes to standard error for its progress meter, measured on
/// Windows on 2026-09-26 with a ten-byte <c>file://</c> source and standard error
/// redirected to a file.
/// </summary>
/// <remarks>
/// These are the fixed lines: the two header lines, and the first status line, whose
/// counters are all zero, which is every byte curl writes for a <c>file://</c> transfer.
/// The status lines curl draws after it from a transfer's byte counts come from
/// <see cref="TransferProgressRecorder" />.
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
    /// The offset curl 8.21.0 resumes a <c>-T</c> upload from under <c>-C -</c>: -1, the
    /// server's to know, which its resuming line names (measured 2026-09-27, BL-351 Notes).
    /// </summary>
    internal const long UnknownUploadOffset = -1;

    /// <summary>
    /// The lines curl writes before the meter's first status line, in the order it writes
    /// them, each without a terminator.
    /// </summary>
    /// <param name="resumeFrom">
    /// The resolved <c>-C</c> offset, <see cref="UnknownUploadOffset" /> for a <c>-T</c> upload
    /// under <c>-C -</c>, or <see langword="null" /> without <c>-C</c>. Past zero, or unknown,
    /// <see cref="ResumingLine" /> comes first.
    /// </param>
    /// <returns>The lines.</returns>
    internal static IReadOnlyList<string> HeaderLines(long? resumeFrom) =>
        resumeFrom is (> 0 or UnknownUploadOffset) and long position
            ? [ResumingLine(position), FirstHeaderLine, SecondHeaderLine]
            : [FirstHeaderLine, SecondHeaderLine];
}
