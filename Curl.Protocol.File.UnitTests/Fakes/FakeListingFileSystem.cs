using Curl.Protocol.Abstractions;

namespace Curl.Protocol.File.Fakes;

/// <summary>
/// A <see cref="FakeFileSystem" /> that can also list directories, as the real disk can:
/// opening one of its directories fails with <see cref="FileAccessStatus.IsDirectory" />
/// carrying the directory's timestamp, and <see cref="ListEntryNamesAsync" /> hands back
/// the entry names it was given, in order.
/// </summary>
public sealed class FakeListingFileSystem : IFileSystem, IDirectoryLister
{
    private readonly FakeFileSystem files = new();
    private readonly Dictionary<string, (DateTimeOffset? LastWriteTimeUtc, IReadOnlyList<string>? Names)> directories =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the paths <see cref="ListEntryNamesAsync" /> was asked to list, in order.
    /// </summary>
    public List<string> ListedPaths { get; } = [];

    /// <summary>
    /// Adds a directory.
    /// </summary>
    /// <param name="path">The operating-system path.</param>
    /// <param name="lastWriteTimeUtc">The timestamp its open reports, or <see langword="null" /> for unknown.</param>
    /// <param name="names">The entry names a listing gives, or <see langword="null" /> for one that cannot be listed.</param>
    public void AddDirectory(string path, DateTimeOffset? lastWriteTimeUtc, IReadOnlyList<string>? names)
    {
        ArgumentNullException.ThrowIfNull(path);

        directories[path] = (lastWriteTimeUtc, names);
    }

    /// <inheritdoc />
    public ValueTask<FileOpenResult> OpenForReadAsync(string path, CancellationToken cancellationToken) =>
        directories.TryGetValue(path, out var directory)
            ? ValueTask.FromResult(
                FileOpenResult.Failed(FileAccessStatus.IsDirectory) with { LastWriteTimeUtc = directory.LastWriteTimeUtc })
            : files.OpenForReadAsync(path, cancellationToken);

    /// <inheritdoc />
    public ValueTask<FileOpenResult> OpenForWriteAsync(
        string path,
        FileWriteMode mode,
        UnixFileMode createMode,
        CancellationToken cancellationToken) =>
        files.OpenForWriteAsync(path, mode, createMode, cancellationToken);

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<string>?> ListEntryNamesAsync(string path, CancellationToken cancellationToken)
    {
        ListedPaths.Add(path);

        return ValueTask.FromResult(directories.TryGetValue(path, out var directory) ? directory.Names : null);
    }
}
