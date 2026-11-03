using Curl.Protocol.Abstractions;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.Processes;

/// <summary>
/// A stand-in process starter whose program takes what curl writes and never writes a byte back, until curl
/// kills it, so a <c>-m</c> case reaches its deadline on the injected clock with no real process.
/// </summary>
/// <param name="environment">The variables the program search reads (<c>PATH</c>, <c>PATHEXT</c>).</param>
internal sealed class SilentProcessStarter(IReadOnlyDictionary<string, string> environment) : IExtensionProcessStarter
{
    private readonly TaskCompletionSource reading = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Completes once curl waits to read the program's output.</summary>
    public Task Reading => reading.Task;

    public string? ReadEnvironmentVariable(string name) => environment.GetValueOrDefault(name);

    public IExtensionProcess Start(string program, IReadOnlyList<string> arguments) => new SilentProcess(reading);

    private sealed class SilentProcess(TaskCompletionSource reading) : IExtensionProcess
    {
        private readonly TaskCompletionSource killed = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Stream StandardInput { get; } = new MemoryStream();

        public Stream StandardOutput => new SilentOutput(reading, killed.Task);

        public async Task<int> WaitForExitAsync(CancellationToken cancellationToken)
        {
            await killed.Task.WaitAsync(cancellationToken);
            return -1;
        }

        public void Kill() => killed.TrySetResult();

        public void Dispose() => killed.TrySetResult();
    }

    /// <summary>An output that never yields a byte: a read waits until the program is killed (end of stream) or the read is cancelled.</summary>
    private sealed class SilentOutput(TaskCompletionSource reading, Task killed) : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            reading.TrySetResult();
            await killed.WaitAsync(cancellationToken);
            return 0;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
