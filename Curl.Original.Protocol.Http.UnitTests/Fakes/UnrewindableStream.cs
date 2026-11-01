namespace Curl.Original.Protocol.Http.Fakes;

/// <summary>
/// A body stream that cannot seek and that curl could not rewind either
/// (<see cref="IRewindableUpload.CanRewind" /> is <see langword="false" />), as standard input
/// read through a pipe off Windows is (BL-2337).
/// </summary>
/// <param name="content">The bytes it reads.</param>
public sealed class UnrewindableStream(byte[] content) : MemoryStream(content), IRewindableUpload
{
    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public bool CanRewind => false;
}
