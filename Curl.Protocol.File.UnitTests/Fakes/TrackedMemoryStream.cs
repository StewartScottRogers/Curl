namespace Curl.Protocol.File.Fakes;

/// <summary>
/// A seekable in-memory stream that remembers whether it was disposed. Stands in both
/// for the seekable handle a read open contractually hands back and for the capture
/// buffer a write open fills.
/// </summary>
public sealed class TrackedMemoryStream : MemoryStream, IRecordingStream
{
    /// <summary>
    /// Creates an empty, expandable stream positioned at zero.
    /// </summary>
    public TrackedMemoryStream()
    {
    }

    /// <summary>
    /// Creates an expandable stream holding <paramref name="content" />, positioned at
    /// zero.
    /// </summary>
    /// <param name="content">The initial content.</param>
    public TrackedMemoryStream(byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);

        Write(content, 0, content.Length);
        Position = 0;
    }

    /// <inheritdoc />
    public bool WasDisposed { get; private set; }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        WasDisposed = true;

        base.Dispose(disposing);
    }
}
