using System.Text;

namespace Curl.Extended.Protocol.McpClient.Stdio;

/// <summary>
/// A server process made of in-memory streams: standard output holds <paramref name="serverOutput"/>,
/// standard input records what the client writes, and the process exits when standard input closes
/// only if <paramref name="exitsWhenInputCloses"/>.
/// </summary>
internal sealed class InMemoryMcpServerProcess(string serverOutput, bool exitsWhenInputCloses = true) : IMcpServerProcess
{
    private readonly RecordingStream input = new();

    public Stream StandardInput => input;

    public Stream StandardOutput { get; } = new MemoryStream(Encoding.UTF8.GetBytes(serverOutput));

    public string WrittenInput => Encoding.UTF8.GetString(input.ToArray());

    public bool InputClosed => input.Closed;

    public bool InputClosedBeforeExitWait { get; private set; }

    public bool Killed { get; private set; }

    public bool Disposed { get; private set; }

    public Task WaitForExitAsync(CancellationToken cancellationToken)
    {
        InputClosedBeforeExitWait = input.Closed;
        return exitsWhenInputCloses ? Task.CompletedTask : Task.Delay(Timeout.Infinite, cancellationToken);
    }

    public void Kill() => Killed = true;

    public void Dispose() => Disposed = true;

    private sealed class RecordingStream : MemoryStream
    {
        public bool Closed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            Closed = true;
            base.Dispose(disposing);
        }
    }
}
