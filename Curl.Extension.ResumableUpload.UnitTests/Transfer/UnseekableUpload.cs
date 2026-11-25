namespace Curl.Extension.ResumableUpload.Transfer;

/// <summary>
/// A non-seekable upload, as standard input is: it gives its content once, in the sizes it is asked for, and then ends
/// or fails.
/// </summary>
/// <param name="content">What it gives.</param>
/// <param name="failure">What it throws once <paramref name="content" /> is read, or <see langword="null" /> to end there.</param>
internal sealed class UnseekableUpload(byte[] content, Exception? failure = null) : MemoryStream(content)
{
    public override bool CanSeek => false;

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        Position == Length && failure is not null ? ValueTask.FromException<int>(failure) : new(Read(buffer.Span));
}
