namespace Curl.Protocol.Imap.Fakes;

/// <summary>
/// An output that refuses every write after the first <see cref="WritesBeforeFailure" /> with
/// <paramref name="failure" />, so the exit 23 path runs with no disk involved.
/// </summary>
/// <param name="failure">What each refused write throws.</param>
public sealed class FailingOutputStream(IOException failure) : Stream
{
    private int writesAccepted;

    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <summary>Gets or sets how many asynchronous writes succeed before the first is refused; 0 by default.</summary>
    public int WritesBeforeFailure { get; set; }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw failure;

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (writesAccepted < WritesBeforeFailure)
        {
            writesAccepted++;
            return ValueTask.CompletedTask;
        }

        return ValueTask.FromException(failure);
    }
}
