namespace Curl.Protocol.Ssh.Fakes;

/// <summary>
/// Stands in for standard input as an upload's source: bytes that cannot be sought, so
/// their size is unknown.
/// </summary>
/// <param name="bytes">The bytes the stream reads.</param>
internal sealed class UnseekableStream(byte[] bytes) : MemoryStream(bytes)
{
    /// <inheritdoc />
    public override bool CanSeek => false;
}
