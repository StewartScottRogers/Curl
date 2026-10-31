using System.Text;

namespace Curl.Extension.Protocol.Plugin;

/// <summary>A helper that answers with recorded bytes and keeps what Curl wrote to it.</summary>
/// <param name="reply">Everything the helper writes to its standard output.</param>
/// <param name="exitStatus">The status it exits with.</param>
internal sealed class ScriptedHelperProcess(string reply, int exitStatus = 0) : IHelperProcess
{
    private readonly MemoryStream input = new();

    public Stream Input => input;

    public Stream Output { get; } = new MemoryStream(Encoding.UTF8.GetBytes(reply));

    public bool IsDisposed { get; private set; }

    /// <summary>Gets what Curl wrote to the helper's standard input.</summary>
    public string Received => Encoding.UTF8.GetString(input.ToArray());

    public Task<int> WaitForExitAsync(CancellationToken cancellationToken) => Task.FromResult(exitStatus);

    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }
}
