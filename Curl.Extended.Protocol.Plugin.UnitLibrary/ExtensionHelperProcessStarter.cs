using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Plugin;

/// <summary>Starts helpers through the process starter the host gave the extensions (ADR-1184).</summary>
/// <param name="starter">The host's process starter.</param>
internal sealed class ExtensionHelperProcessStarter(IExtensionProcessStarter starter) : IHelperProcessStarter
{
    /// <inheritdoc />
    public IHelperProcess Start(string program, string url) => new ExtensionHelperProcess(starter.Start(program, [url]));

    /// <summary>A helper seen through the host's process; disposing it ends the helper if it still runs.</summary>
    /// <param name="process">The host's process.</param>
    private sealed class ExtensionHelperProcess(IExtensionProcess process) : IHelperProcess
    {
        public Stream Input => process.StandardInput;

        public Stream Output => process.StandardOutput;

        public Task<int> WaitForExitAsync(CancellationToken cancellationToken) => process.WaitForExitAsync(cancellationToken);

        public ValueTask DisposeAsync()
        {
            process.Kill();
            process.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
