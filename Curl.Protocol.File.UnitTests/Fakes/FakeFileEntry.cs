namespace Curl.Protocol.File.Fakes;

/// <summary>
/// One entry in <see cref="FakeFileSystem" />: a file, a directory, or a file whose
/// handle the test supplied itself.
/// </summary>
/// <param name="Content">The bytes of a file, or empty for a directory.</param>
/// <param name="Length">The length the open reports.</param>
/// <param name="LastWriteTimeUtc">The timestamp the open reports.</param>
/// <param name="IsDirectory">Whether opening this entry for reading fails as a directory.</param>
/// <param name="PresetContent">
/// A stream the test supplied for the open to hand back — a faulting one, for instance —
/// or <see langword="null" /> to have the fake build a seekable one from
/// <paramref name="Content" />.
/// </param>
public sealed record FakeFileEntry(
    byte[] Content,
    long Length,
    DateTimeOffset LastWriteTimeUtc,
    bool IsDirectory,
    Stream? PresetContent)
{
    /// <summary>
    /// Creates a readable file entry.
    /// </summary>
    /// <param name="content">The file's bytes.</param>
    /// <param name="lastWriteTimeUtc">The file's timestamp.</param>
    /// <returns>The entry.</returns>
    public static FakeFileEntry ForFile(byte[] content, DateTimeOffset lastWriteTimeUtc)
    {
        ArgumentNullException.ThrowIfNull(content);

        return new FakeFileEntry(content, content.Length, lastWriteTimeUtc, false, null);
    }

    /// <summary>
    /// Creates a directory entry, which a read open rejects.
    /// </summary>
    /// <returns>The entry.</returns>
    public static FakeFileEntry ForDirectory() => new([], 0, default, true, null);

    /// <summary>
    /// Creates a file entry whose read open hands back a stream the test owns.
    /// </summary>
    /// <param name="content">The stream to hand back.</param>
    /// <param name="length">The length the open reports.</param>
    /// <param name="lastWriteTimeUtc">The timestamp the open reports.</param>
    /// <returns>The entry.</returns>
    public static FakeFileEntry ForStream(
        Stream content,
        long length,
        DateTimeOffset lastWriteTimeUtc)
    {
        ArgumentNullException.ThrowIfNull(content);

        return new FakeFileEntry([], length, lastWriteTimeUtc, false, content);
    }
}
