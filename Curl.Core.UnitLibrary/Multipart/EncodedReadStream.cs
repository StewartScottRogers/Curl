using System.Buffers;
using Curl.Protocol.Abstractions;

namespace Curl.Core.Multipart;

/// <summary>
/// A read-only stream that encodes a part's data as it reads it, so a file part under
/// <c>base64</c>, <c>quoted-printable</c> or <c>7bit</c> is never held in memory whole, as
/// libcurl 8.21.0's <c>lib/mime.c</c> encoders read. It disposes its source when disposed.
/// </summary>
/// <remarks>
/// It seeks when its source seeks, by starting the encoding again from where the source stood
/// when the stream was made and reading forward, which is what lets
/// <see cref="ConcatenatedReadStream" /> send a body again after a 307 or 308. A read that
/// reaches data the encoder refuses throws <see cref="RequestBodyReadFailedException" />.
/// </remarks>
internal sealed class EncodedReadStream : Stream
{
    private const int InputBufferSize = 64 * 1024;

    private const int SkipBufferSize = 16 * 1024;

    private readonly Stream source;

    private readonly Func<MultipartDataEncoding> createEncoding;

    private readonly long sourceStart;

    private readonly byte[] input = new byte[InputBufferSize];

    private readonly ArrayBufferWriter<byte> output = new();

    private MultipartDataEncoding encoding;

    private long? length;

    private int inputCount;

    private int outputOffset;

    private bool sourceEnded;

    private long position;

    /// <summary>Initialises a stream that encodes <paramref name="source" />, which it now owns.</summary>
    /// <param name="source">The part's data.</param>
    /// <param name="createEncoding">Starts an encoding of the data from its beginning.</param>
    /// <param name="length">The encoded size in bytes when it is known beforehand; otherwise it is measured when asked for.</param>
    internal EncodedReadStream(Stream source, Func<MultipartDataEncoding> createEncoding, long? length)
    {
        this.source = source;
        this.createEncoding = createEncoding;
        this.length = length;
        sourceStart = source.CanSeek ? source.Position : 0;
        encoding = createEncoding();
    }

    /// <inheritdoc />
    public override bool CanRead => true;

    /// <inheritdoc />
    public override bool CanSeek => source.CanSeek;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    /// <remarks>An encoded size not known beforehand is measured by encoding the data once, without keeping it.</remarks>
    public override long Length => length ??= MeasureLength();

    /// <inheritdoc />
    public override long Position
    {
        get => position;
        set => MoveTo(value);
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    /// <inheritdoc />
    public override int Read(Span<byte> buffer)
    {
        while (OutputIsEmpty && !sourceEnded)
        {
            EncodeInput(source.Read(input.AsSpan(inputCount)));
        }

        return CopyOutput(buffer);
    }

    /// <inheritdoc />
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc />
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        while (OutputIsEmpty && !sourceEnded)
        {
            EncodeInput(await source.ReadAsync(input.AsMemory(inputCount), cancellationToken).ConfigureAwait(false));
        }

        return CopyOutput(buffer.Span);
    }

    /// <inheritdoc />
    public override void Flush()
    {
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin)
    {
        Position = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => Position + offset,
            SeekOrigin.End => Length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin)),
        };
        return Position;
    }

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    /// <remarks>
    /// The source is disposed whatever <paramref name="disposing" /> says: this sealed type
    /// declares no finalizer, so only <see cref="Stream.Dispose()" /> ever calls this.
    /// </remarks>
    protected override void Dispose(bool disposing)
    {
        source.Dispose();
        base.Dispose(disposing);
    }

    private bool OutputIsEmpty => outputOffset == output.WrittenCount;

    /// <summary>
    /// Takes the <paramref name="read" /> bytes just read into the input, where a read of none
    /// is the end of the data, and encodes what the encoding can, keeping the rest for the next read.
    /// </summary>
    private void EncodeInput(int read)
    {
        inputCount += read;
        sourceEnded = read == 0;
        output.ResetWrittenCount();
        outputOffset = 0;
        if (!encoding.TryEncode(input.AsSpan(0, inputCount), sourceEnded, output, out int consumed))
        {
            throw new RequestBodyReadFailedException(MultipartFormBodyBuilder.ReadFailedMessage);
        }

        inputCount -= consumed;
        input.AsSpan(consumed, inputCount).CopyTo(input);
    }

    private int CopyOutput(Span<byte> buffer)
    {
        int count = Math.Min(buffer.Length, output.WrittenCount - outputOffset);
        output.WrittenSpan.Slice(outputOffset, count).CopyTo(buffer);
        outputOffset += count;
        position += count;
        return count;
    }

    /// <summary>
    /// Starts the encoding again from the source's start and reads forward to
    /// <paramref name="target" />, or to the end when the data is shorter.
    /// </summary>
    private void MoveTo(long target)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(target);
        source.Position = sourceStart;
        encoding = createEncoding();
        inputCount = 0;
        output.ResetWrittenCount();
        outputOffset = 0;
        sourceEnded = false;
        position = 0;

        byte[] skipped = new byte[SkipBufferSize];
        int read = 1;
        while (position < target && read > 0)
        {
            read = Read(skipped.AsSpan(0, (int)Math.Min(SkipBufferSize, target - position)));
        }
    }

    private long MeasureLength()
    {
        long current = position;
        MoveTo(long.MaxValue);
        long measured = position;
        MoveTo(current);
        return measured;
    }
}
