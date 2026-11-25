namespace Curl.Protocol.Abstractions;

/// <summary>
/// Replaces a whole file through an <see cref="IFileSystem" />, as curl saves the files that
/// hold its state: the cookie jar, the HSTS cache and the alt-svc cache.
/// </summary>
public static class FileSystemReplacement
{
    /// <summary>
    /// Replaces the file at <paramref name="path" /> with <paramref name="content" /> under
    /// <see cref="FileWriteMode.Replace" />.
    /// </summary>
    /// <param name="fileSystem">Opens the file.</param>
    /// <param name="path">The file to replace.</param>
    /// <param name="content">Every byte the file is to hold.</param>
    /// <param name="createMode">The mode a file that did not exist is created with, before the umask.</param>
    /// <param name="cancellationToken">Cancels the open and the write.</param>
    /// <returns>
    /// <see langword="true" /> when the file was replaced; <see langword="false" /> when it could
    /// not be opened, written or replaced, which curl does not report.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="fileSystem" /> or <paramref name="path" /> is <see langword="null" />.</exception>
    public static async Task<bool> TryReplaceFileAsync(
        this IFileSystem fileSystem,
        string path,
        ReadOnlyMemory<byte> content,
        UnixFileMode createMode,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(path);

        FileOpenResult opened = await fileSystem.OpenForWriteAsync(path, FileWriteMode.Replace, createMode, cancellationToken).ConfigureAwait(false);
        if (opened.Content is not { } stream)
        {
            return false;
        }

        // A close that fails (a full device takes the bytes into its buffer and fails as it
        // closes) is a file that cannot be written too (CGR-026, BL-3830).
        try
        {
            await using (stream.ConfigureAwait(false))
            {
                await stream.WriteAsync(content, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }
}
