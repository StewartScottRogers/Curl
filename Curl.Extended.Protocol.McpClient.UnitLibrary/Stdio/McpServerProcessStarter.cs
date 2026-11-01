using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Curl.Extended.Protocol.McpClient.Stdio;

/// <summary>
/// Starts the MCP server with <see cref="Process"/>: standard input and output redirected, standard
/// error left on the client's own standard error, where the server's log belongs ("Base Protocol &gt;
/// Transports &gt; stdio").
/// </summary>
/// <remarks>
/// Every line starts or drives a real process, so per ADR-0083 and ADR-1031 the type is left out of
/// the fast-run coverage measure; everything above the seam is unit tested with in-memory streams.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "ADR-1031: a thin System.Diagnostics.Process adapter.")]
public sealed class McpServerProcessStarter : IMcpServerProcessStarter
{
    /// <inheritdoc/>
    public IMcpServerProcess Start(string command, IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(arguments);
        var startInfo = new ProcessStartInfo(command)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = false,
            StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"The MCP server '{command}' did not start.");
        return new SystemMcpServerProcess(process);
    }

    private sealed class SystemMcpServerProcess(Process process) : IMcpServerProcess
    {
        public Stream StandardInput => process.StandardInput.BaseStream;

        public Stream StandardOutput => process.StandardOutput.BaseStream;

        public Task WaitForExitAsync(CancellationToken cancellationToken) =>
            process.WaitForExitAsync(cancellationToken);

        public void Kill() => process.Kill(entireProcessTree: true);

        public void Dispose() => process.Dispose();
    }
}
