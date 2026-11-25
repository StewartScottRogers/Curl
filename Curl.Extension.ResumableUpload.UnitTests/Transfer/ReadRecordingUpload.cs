namespace Curl.Extension.ResumableUpload.Transfer;

/// <summary>
/// A seekable upload of a given length that holds no content in memory: each byte is its position modulo 251, and every
/// byte read is counted, so a test can see what was read of an upload of any size.
/// </summary>
/// <param name="length">The upload's length.</param>
internal sealed class ReadRecordingUpload(long length) : Stream
{
    /// <summary>Gets how many bytes have been read.</summary>
    public long BytesRead { get; private set; }

    public override bool CanRead => true;

    public override bool CanSeek => true;

    public override bool CanWrite => false;

    public override long Length => length;

    public override long Position { get; set; }

    /// <summary>Gives the byte at <paramref name="position" />.</summary>
    public static byte ByteAt(long position) => (byte)(position % 251);

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        var count = (int)Math.Clamp(length - Position, 0, buffer.Length);
        for (var index = 0; index < count; index++)
        {
            buffer[index] = ByteAt(Position++);
        }

        BytesRead += count;
        return count;
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => new(Read(buffer.Span));

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        Task.FromResult(Read(buffer, offset, count));

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void Flush()
    {
    }

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
