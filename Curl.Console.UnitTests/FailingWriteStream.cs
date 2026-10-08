namespace Curl.Console;

/// <summary>
/// A memory stream standing in for a closed standard output: its first
/// <see cref="WritesToFail" /> writes, and every flush while <see cref="FailsFlush" /> is
/// set, throw an <see cref="IOException" />; the rest behave as a <see cref="MemoryStream" />.
/// </summary>
internal sealed class FailingWriteStream : MemoryStream
{
    /// <summary>Gets or sets how many writes, from the next one, throw.</summary>
    public int WritesToFail { get; set; } = int.MaxValue;

    /// <summary>Gets or sets a value indicating whether a flush throws.</summary>
    public bool FailsFlush { get; set; }

    public override void Write(byte[] buffer, int offset, int count)
    {
        ThrowIfWriteFails();
        base.Write(buffer, offset, count);
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        ThrowIfWriteFails();
        base.Write(buffer);
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ThrowIfWriteFails();

        return base.WriteAsync(buffer, cancellationToken);
    }

    public override void Flush()
    {
        ThrowIfFlushFails();
        base.Flush();
    }

    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        ThrowIfFlushFails();

        return base.FlushAsync(cancellationToken);
    }

    private void ThrowIfWriteFails()
    {
        if (WritesToFail > 0)
        {
            WritesToFail--;
            throw new IOException("The pipe is being closed.");
        }
    }

    private void ThrowIfFlushFails()
    {
        if (FailsFlush)
        {
            throw new IOException("The pipe is being closed.");
        }
    }
}
