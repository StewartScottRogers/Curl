using System.Globalization;

namespace Curl.Conformance;

/// <summary>
/// The two environment variables a developer sets to debug upstream cases (BL-2155):
/// <c>CURL_UPSTREAM_CASE</c> names one case for the manual test to run, listed or not, and
/// <c>CURL_UPSTREAM_KEEP_LOGS=1</c> keeps every case's log folder instead of deleting it.
/// </summary>
/// <param name="CaseNumber">The case <c>CURL_UPSTREAM_CASE</c> names, or <see langword="null"/> when it is unset or not a case number.</param>
/// <param name="KeepsLogs">Whether <c>CURL_UPSTREAM_KEEP_LOGS</c> is <c>1</c>.</param>
public sealed record UpstreamCaseDebugSettings(int? CaseNumber, bool KeepsLogs)
{
    /// <summary>The environment variable that names the one case the manual test runs.</summary>
    public const string CaseVariableName = "CURL_UPSTREAM_CASE";

    /// <summary>The environment variable that, set to <c>1</c>, keeps each case's log folder.</summary>
    public const string KeepLogsVariableName = "CURL_UPSTREAM_KEEP_LOGS";

    /// <summary>Reads the settings from the environment.</summary>
    /// <param name="readEnvironmentVariable">Returns a variable's value, or <see langword="null"/> when it is unset.</param>
    /// <returns>The case named, if any, and whether log folders are kept.</returns>
    public static UpstreamCaseDebugSettings Read(Func<string, string?> readEnvironmentVariable)
    {
        ArgumentNullException.ThrowIfNull(readEnvironmentVariable);
        int? caseNumber = int.TryParse(readEnvironmentVariable(CaseVariableName)?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int number)
            ? number
            : null;
        return new UpstreamCaseDebugSettings(caseNumber, readEnvironmentVariable(KeepLogsVariableName) == "1");
    }

    /// <summary>The line printed when a case's log folder is kept.</summary>
    /// <param name="logDirectory">The kept folder's full path.</param>
    /// <returns><c>Kept the case's log folder: &lt;path&gt;</c>.</returns>
    public static string KeptLogFolderLine(string logDirectory) => $"Kept the case's log folder: {logDirectory}";
}
