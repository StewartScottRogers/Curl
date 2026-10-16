namespace Curl.Mcp;

/// <summary>
/// Runs one curl command line in-process for the <c>curl</c> tool (ADR-0548, ADR-0569). <c>Curl.Mcp</c>
/// adapts <c>Curl.Console.InProcessCurl</c> to it, so this library never references the executable.
/// </summary>
public interface ICurlCommandRunner
{
    /// <summary>
    /// Runs curl with <paramref name="arguments" /> and gives its exit code.
    /// </summary>
    /// <param name="arguments">The command line without the program name, exactly as the tool was given it.</param>
    /// <param name="standardInput">What curl reads as standard input.</param>
    /// <param name="standardOutput">Where curl writes standard output.</param>
    /// <param name="standardError">Where curl writes standard error.</param>
    /// <param name="workingDirectory">The folder relative paths resolve against, <c>CURL_MCP_ROOT</c>; <see langword="null" /> when no root is set. The runner never changes the process-wide current directory.</param>
    /// <param name="cancellationToken">The MCP request's cancellation token.</param>
    /// <returns>curl's exit code.</returns>
    Task<int> RunAsync(
        IReadOnlyList<string> arguments,
        Stream standardInput,
        Stream standardOutput,
        Stream standardError,
        string? workingDirectory,
        CancellationToken cancellationToken);
}
