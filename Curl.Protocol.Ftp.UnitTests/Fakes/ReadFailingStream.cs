namespace Curl.Protocol.Ftp.Fakes;

/// <summary>
/// An upload source whose every read throws an <see cref="IOException" />, as a file on a
/// failed disk does.
/// </summary>
public sealed class ReadFailingStream : MemoryStream
{
    /// <inheritdoc />
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        throw new IOException("The device is not ready.");
}
