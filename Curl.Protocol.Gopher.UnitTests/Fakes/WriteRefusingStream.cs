namespace Curl.Protocol.Gopher.Fakes;

/// <summary>
/// An output stream that refuses every write with an <see cref="IOException" />, as a full
/// disk or a closed pipe does.
/// </summary>
public sealed class WriteRefusingStream : MemoryStream
{
    /// <inheritdoc />
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
        throw new IOException("The destination is full.");
}
