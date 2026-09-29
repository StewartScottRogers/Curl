namespace Curl.Tls;

/// <summary>A transport that reads back fixed server bytes and records what the client writes; writes can be made to fail.</summary>
internal sealed class ScriptedTransport(byte[] serverBytes) : Stream
{
    private readonly MemoryStream input = new(serverBytes);
    private readonly MemoryStream output = new();

    public bool FailWrites { get; set; }

    public bool IsDisposed { get; private set; }

    public int FlushCount { get; private set; }

    public byte[] Written => output.ToArray();

    public override bool CanRead => true;

    public override bool CanWrite => true;

    public override bool CanSeek => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        input.ReadAsync(buffer, cancellationToken);

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (FailWrites)
        {
            throw new IOException("The transport is closed.");
        }

        output.Write(buffer.Span);
        return ValueTask.CompletedTask;
    }

    public override void Flush() => FlushCount++;

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        IsDisposed = true;
        base.Dispose(disposing);
    }
}
