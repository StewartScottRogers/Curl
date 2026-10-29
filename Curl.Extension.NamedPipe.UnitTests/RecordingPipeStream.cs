namespace Curl.Extension.NamedPipe;

/// <summary>The client end of a byte-mode pipe with no OS behind it: reads give the server's bytes, writes are kept.</summary>
internal sealed class RecordingPipeStream(byte[] serverBytes) : Stream
{
    private readonly MemoryStream fromServer = new(serverBytes);

    /// <summary>Gets the bytes the client wrote.</summary>
    public MemoryStream Written { get; } = new();

    /// <summary>Gets how many times the client flushed.</summary>
    public int FlushCount { get; private set; }

    /// <summary>Gets a value indicating whether the client closed the pipe.</summary>
    public bool IsDisposed { get; private set; }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => throw new NotSupportedException();

    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public override void Flush() => FlushCount++;

    public override int Read(byte[] buffer, int offset, int count) => fromServer.Read(buffer, offset, count);

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => Written.Write(buffer, offset, count);

    public override ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return base.DisposeAsync();
    }
}
