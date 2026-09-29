namespace Curl.Protocol.Http.Fakes;

/// <summary>
/// A body stream that cannot seek, so its length is unknown, as standard input's is.
/// </summary>
/// <param name="content">The bytes it reads.</param>
public sealed class UnseekableStream(byte[] content) : MemoryStream(content)
{
    /// <inheritdoc />
    public override bool CanSeek => false;
}
