namespace Curl.Original.Protocol.Http.Fakes;

/// <summary>
/// A request body stream whose first read stays pending until <see cref="Unpause" />, as a libcurl
/// read callback that returns <c>CURL_READFUNC_PAUSE</c> keeps it; that read then gives the whole
/// content, and the next read gives 0. A read cancelled while pending is counted in
/// <see cref="ReadCanceled" />.
/// </summary>
/// <param name="content">The bytes the first read gives once unpaused.</param>
public sealed class PausedReadStream(byte[] content) : Stream
{
    private readonly TaskCompletionSource unpaused = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private bool contentGiven;

    /// <summary>Gets a value indicating whether a read was cancelled while it waited for <see cref="Unpause" />.</summary>
    public bool ReadCanceled { get; private set; }

    /// <inheritdoc />
    public override bool CanRead => true;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <summary>Lets the pending read, and every later one, go on.</summary>
    public void Unpause() => unpaused.TrySetResult();

    /// <inheritdoc />
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        try
        {
            await unpaused.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            ReadCanceled = true;
            throw;
        }

        if (contentGiven)
        {
            return 0;
        }

        contentGiven = true;
        content.CopyTo(buffer);
        return content.Length;
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Flush()
    {
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
