namespace Curl.Mcp;

/// <summary>
/// What the <c>curl</c> tool gives back (ADR-0548, ADR-0569): curl's standard output, standard
/// error and exit code when it ran, and a tool error when it exited nonzero, was refused before it
/// ran, or was cancelled.
/// </summary>
/// <param name="ExitCode">curl's exit code; <see langword="null" /> when curl did not run or was cancelled.</param>
/// <param name="StandardOutput">curl's standard output, cut to the cap; <see langword="null" /> when curl did not run or was cancelled.</param>
/// <param name="StandardError">curl's standard error, cut to the cap; <see langword="null" /> when curl did not run or was cancelled.</param>
/// <param name="Failure">The tool error; <see langword="null" /> when curl exited 0.</param>
public sealed record McpCurlToolResult(int? ExitCode, McpReturnedBytes? StandardOutput, McpReturnedBytes? StandardError, McpToolFailure? Failure)
{
    /// <summary>Whether the result is a tool error.</summary>
    public bool IsError => Failure is not null;

    /// <summary>Whether standard output was cut at the cap.</summary>
    public bool IsStandardOutputTruncated => StandardOutput?.IsTruncated ?? false;

    /// <summary>Whether standard error was cut at the cap.</summary>
    public bool IsStandardErrorTruncated => StandardError?.IsTruncated ?? false;

    /// <summary>The result for a run that never reached curl.</summary>
    /// <param name="failure">Why it did not run.</param>
    /// <returns>A result with no output and no exit code.</returns>
    public static McpCurlToolResult NotRun(McpToolFailure failure) => new(null, null, null, failure);
}
