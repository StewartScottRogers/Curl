namespace Curl.Protocol.Mqtt.Fakes;

/// <summary>
/// An output stream that keeps each write as its own chunk, so a test can assert how the
/// handler wrote as well as what; or, with <see cref="FailWrites" />, refuses every write.
/// </summary>
public sealed class RecordingStream : MemoryStream
{
    /// <summary>
    /// Gets each write, in order.
    /// </summary>
    public List<byte[]> Writes { get; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether every write throws an <see cref="IOException" />.
    /// </summary>
    public bool FailWrites { get; set; }

    /// <inheritdoc />
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (FailWrites)
        {
            throw new IOException("The destination is full.");
        }

        Writes.Add(buffer.ToArray());
        return base.WriteAsync(buffer, cancellationToken);
    }
}
