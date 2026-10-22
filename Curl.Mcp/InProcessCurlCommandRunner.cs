using Curl.Console;
using Curl.Networking;

namespace Curl.Mcp;

/// <summary>
/// Runs the <c>curl</c> tool's command line through <see cref="InProcessCurl" /> over the production
/// <see cref="TcpConnector" />: a <see cref="TcpDialer" /> with curl's default socket options, the
/// <see cref="SystemDnsResolver" /> and a <see cref="UdpDatagramConnector" /> (ADR-0548, ADR-0575).
/// </summary>
internal sealed class InProcessCurlCommandRunner : ICurlCommandRunner
{
    /// <summary>
    /// Runs <paramref name="arguments" /> in-process and gives curl's exit code. Relative paths resolve
    /// against the process's current directory, which <see cref="CurlMcpHost" /> set to the root at
    /// start, so this never changes it; <paramref name="cancellationToken" /> is not passed on, because
    /// <see cref="InProcessCurl" /> takes none.
    /// </summary>
    /// <param name="arguments">The command line without the program name.</param>
    /// <param name="standardInput">What curl reads as standard input.</param>
    /// <param name="standardOutput">Where curl writes standard output.</param>
    /// <param name="standardError">Where curl writes standard error.</param>
    /// <param name="workingDirectory">The root, or <see langword="null" /> when none is set; it must be the current directory.</param>
    /// <param name="cancellationToken">The MCP request's cancellation token.</param>
    /// <returns>curl's exit code.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="workingDirectory" /> is not the process's current directory, so relative paths would escape the root.</exception>
    public Task<int> RunAsync(
        IReadOnlyList<string> arguments,
        Stream standardInput,
        Stream standardOutput,
        Stream standardError,
        string? workingDirectory,
        CancellationToken cancellationToken)
    {
        if (workingDirectory is not null && !IsCurrentDirectory(workingDirectory))
        {
            throw new InvalidOperationException($"The working directory '{workingDirectory}' is not the current directory '{Directory.GetCurrentDirectory()}'.");
        }

        SystemDnsResolver dnsResolver = new();
        return InProcessCurl.RunAsync(
            arguments,
            standardOutput,
            standardError,
            standardInput,
            new TcpDialer(new TcpSocketOptions()),
            dnsResolver,
            new UdpDatagramConnector(dnsResolver, TimeProvider.System));
    }

    /// <summary>Whether <paramref name="directory" /> is the process's current directory, ignoring a trailing separator; compared ordinally.</summary>
    private static bool IsCurrentDirectory(string directory) =>
        string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory)),
            Path.TrimEndingDirectorySeparator(Directory.GetCurrentDirectory()),
            StringComparison.Ordinal);
}
