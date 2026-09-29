namespace Curl.Protocol.Smb.Fakes;

/// <summary>A readable stream that cannot seek, standing in for <c>-T -</c>'s standard input.</summary>
/// <param name="content">The bytes the stream reads.</param>
public sealed class UnseekableStream(byte[] content) : MemoryStream(content)
{
    /// <inheritdoc />
    public override bool CanSeek => false;
}
