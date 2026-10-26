namespace Curl.Conformance.Differential.UnitTests.Http.Core;

/// <summary>
/// An in-memory connection: reads come from the given request bytes, writes are collected; reads can be made to fail,
/// and the end of the input can be held back until another task completes, so a relay test does not race its two sides.
/// </summary>
internal sealed class DuplexMemoryStream(byte[] input, bool failsReads = false, Task? endOfInputWaitsFor = null) : Stream
{
    private readonly MemoryStream _input = new(input);
    private readonly MemoryStream _output = new();
    private readonly TaskCompletionSource _inputEnded = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public byte[] Written => _output.ToArray();

    /// <summary>Completes once a read has found the end of the input.</summary>
    public Task InputEnded => _inputEnded.Task;

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => throw new NotSupportedException();

    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (failsReads)
        {
            throw new IOException("connection reset");
        }

        var read = _input.Read(buffer, offset, count);
        if (read == 0)
        {
            _inputEnded.TrySetResult();
        }

        return read;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (endOfInputWaitsFor is not null && !failsReads && _input.Position == _input.Length)
        {
            await endOfInputWaitsFor.WaitAsync(cancellationToken);
        }

        var array = new byte[buffer.Length];
        var read = Read(array, 0, array.Length);
        array.AsMemory(0, read).CopyTo(buffer);
        return read;
    }

    public override void Write(byte[] buffer, int offset, int count) => _output.Write(buffer, offset, count);

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        _input.Dispose();
        _output.Dispose();
        base.Dispose(disposing);
    }
}
