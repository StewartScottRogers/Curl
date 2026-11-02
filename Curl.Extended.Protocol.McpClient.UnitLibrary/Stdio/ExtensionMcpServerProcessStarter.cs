using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.McpClient.Stdio;

/// <summary>Starts MCP servers through the process starter the host gave the extensions (ADR-1183).</summary>
/// <param name="starter">The host's process starter.</param>
internal sealed class ExtensionMcpServerProcessStarter(IExtensionProcessStarter starter) : IMcpServerProcessStarter
{
    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The server could not be started; the host starter's <see cref="IOException" /> is its inner exception.</exception>
    public IMcpServerProcess Start(string command, IReadOnlyList<string> arguments)
    {
        try
        {
            return new ExtensionMcpServerProcess(starter.Start(command, arguments));
        }
        catch (IOException failure)
        {
            throw new InvalidOperationException(failure.Message, failure);
        }
    }

    /// <summary>A server seen through the host's process.</summary>
    /// <param name="process">The host's process.</param>
    private sealed class ExtensionMcpServerProcess(IExtensionProcess process) : IMcpServerProcess
    {
        public Stream StandardInput => process.StandardInput;

        public Stream StandardOutput => process.StandardOutput;

        public Task WaitForExitAsync(CancellationToken cancellationToken) => process.WaitForExitAsync(cancellationToken);

        public void Kill() => process.Kill();

        public void Dispose() => process.Dispose();
    }
}
