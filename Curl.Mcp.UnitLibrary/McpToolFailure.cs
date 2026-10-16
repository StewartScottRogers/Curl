using Curl.Api.Codes;
using Curl.Protocol.Abstractions;

namespace Curl.Mcp;

/// <summary>
/// A tool error as MCP reports it: a result flagged as an error whose text says what failed,
/// never a protocol exception (ADR-0548).
/// </summary>
/// <param name="Kind">Why the tool failed.</param>
/// <param name="Text">The text the tool error carries to the client.</param>
/// <param name="ExitCode">curl's exit code when <paramref name="Kind" /> is <see cref="McpToolFailureKind.CurlExit" />; otherwise <see langword="null" />.</param>
public sealed record McpToolFailure(McpToolFailureKind Kind, string Text, int? ExitCode)
{
    /// <summary>The text of the error a cancelled request reports.</summary>
    public const string CancelledText = "The request was cancelled before curl finished.";

    /// <summary>
    /// The tool error for a curl run that ended with <paramref name="exitCode" />, or
    /// <see langword="null" /> when it is 0, because a run that succeeded is not an error.
    /// </summary>
    /// <param name="exitCode">curl's exit code, the same number as libcurl's <c>CURLcode</c>.</param>
    /// <param name="curlMessage">curl's own error message, as it wrote it to standard error.</param>
    /// <returns>The tool error, with curl's message followed by the exit code and its name; <see langword="null" /> for 0.</returns>
    public static McpToolFailure? FromCurlExit(int exitCode, string curlMessage)
    {
        if (exitCode == 0)
        {
            return null;
        }

        string codeLine = $"curl exit code {exitCode} ({(CurlCode)exitCode})";
        string message = curlMessage.Trim();
        string text = message.Length == 0 ? codeLine : $"{message}\n{codeLine}";
        return new McpToolFailure(McpToolFailureKind.CurlExit, text, exitCode);
    }

    /// <summary>
    /// The tool error for a curl run that ended with <paramref name="exitCode" />, or
    /// <see langword="null" /> for <see cref="CurlExitCode.Ok" />.
    /// </summary>
    /// <param name="exitCode">curl's exit code.</param>
    /// <param name="curlMessage">curl's own error message, as it wrote it to standard error.</param>
    /// <returns>The tool error; <see langword="null" /> for <see cref="CurlExitCode.Ok" />.</returns>
    public static McpToolFailure? FromCurlExit(CurlExitCode exitCode, string curlMessage) =>
        FromCurlExit((int)exitCode, curlMessage);

    /// <summary>
    /// The tool error for a path an option names that lies outside <c>CURL_MCP_ROOT</c>.
    /// </summary>
    /// <param name="option">The option that carried the path, as given, e.g. <c>--output</c>.</param>
    /// <param name="reason">Why the path was refused.</param>
    /// <returns>The tool error naming the option.</returns>
    public static McpToolFailure PathRefused(string option, string reason) =>
        new(McpToolFailureKind.PathRefused, $"Refused {option}: {reason}", null);

    /// <summary>The tool error for a request cancelled before curl finished.</summary>
    public static McpToolFailure Cancelled { get; } = new(McpToolFailureKind.Cancelled, CancelledText, null);
}
