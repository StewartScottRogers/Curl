using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Plugin;

/// <summary>A host's process made of in-memory streams: its output is <paramref name="reply" /> and it exits with <paramref name="exitStatus" /> at once.</summary>
/// <param name="reply">Everything it writes to its standard output.</param>
/// <param name="exitStatus">The status it exits with.</param>
internal sealed class StandInExtensionProcess(string reply, int exitStatus = 0) : IExtensionProcess
{
    public Stream StandardInput { get; } = new MemoryStream();

    public Stream StandardOutput { get; } = new MemoryStream(Encoding.UTF8.GetBytes(reply));

    public bool IsKilled { get; private set; }

    public bool IsDisposed { get; private set; }

    public Task<int> WaitForExitAsync(CancellationToken cancellationToken) => Task.FromResult(exitStatus);

    public void Kill() => IsKilled = true;

    public void Dispose() => IsDisposed = true;
}
