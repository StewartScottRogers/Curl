using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Mqtt.Fakes;

/// <summary>
/// An output stream that keeps each write as its own chunk, so a test can assert how the
/// handler wrote as well as what; or, with <see cref="FailWrites" />, refuses every write;
/// or, with <see cref="FailingWriteNumber" />, refuses one write with an
/// <see cref="OutputWriteFailedException" />.
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

    /// <summary>
    /// Gets or sets the one-based number of the write that throws an
    /// <see cref="OutputWriteFailedException" /> carrying <see cref="BytesAcceptedOnFailure" />,
    /// or 0 for none.
    /// </summary>
    public int FailingWriteNumber { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="OutputWriteFailedException.BytesAccepted" /> the write
    /// numbered <see cref="FailingWriteNumber" /> reports.
    /// </summary>
    public int BytesAcceptedOnFailure { get; set; }

    /// <inheritdoc />
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (FailWrites)
        {
            throw new IOException("The destination is full.");
        }

        if (Writes.Count + 1 == FailingWriteNumber)
        {
            throw new OutputWriteFailedException(BytesAcceptedOnFailure, "The destination is full.");
        }

        Writes.Add(buffer.ToArray());
        return base.WriteAsync(buffer, cancellationToken);
    }
}
