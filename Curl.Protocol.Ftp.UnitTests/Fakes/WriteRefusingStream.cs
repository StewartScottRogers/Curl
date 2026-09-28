namespace Curl.Protocol.Ftp.Fakes;

/// <summary>
/// An output stream that refuses every write with <paramref name="refusal" />, as a full
/// disk or a closed pipe does, or as a cancelled transfer does.
/// </summary>
/// <param name="refusal">The exception every write throws.</param>
public sealed class WriteRefusingStream(Exception refusal) : MemoryStream
{
    /// <inheritdoc />
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
        throw refusal;
}
