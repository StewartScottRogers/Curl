namespace Curl.Protocol.Http.Fakes;

/// <summary>
/// A transfer output that keeps what it is given and throws a chosen exception from one
/// chosen write, recording the size of every write offered to it, the failed one included.
/// </summary>
/// <param name="failingWrite">
/// The 1-based number of the write that throws, or 0 for none to throw.
/// </param>
/// <param name="failure">The exception that write throws.</param>
public sealed class FailingWriteStream(int failingWrite, Exception failure) : MemoryStream
{
    /// <summary>Initializes a new instance of the <see cref="FailingWriteStream" /> class that never fails.</summary>
    public FailingWriteStream()
        : this(0, new IOException("Never thrown."))
    {
    }

    /// <summary>Gets the size of every write offered, in order.</summary>
    public List<int> WriteSizes { get; } = [];

    /// <inheritdoc />
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        WriteSizes.Add(buffer.Length);
        if (WriteSizes.Count == failingWrite)
        {
            throw failure;
        }

        return base.WriteAsync(buffer, cancellationToken);
    }
}
