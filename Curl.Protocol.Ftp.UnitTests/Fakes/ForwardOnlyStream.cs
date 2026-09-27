namespace Curl.Protocol.Ftp.Fakes;

/// <summary>
/// An upload source that cannot seek, as standard input cannot, holding
/// <paramref name="content" />.
/// </summary>
/// <param name="content">The bytes the source yields.</param>
public sealed class ForwardOnlyStream(byte[] content) : MemoryStream(content)
{
    /// <inheritdoc />
    public override bool CanSeek => false;
}
