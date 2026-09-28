namespace Curl.Console;

/// <summary>
/// <see cref="IOutputPaths" /> over the real disk.
/// </summary>
internal sealed class PhysicalOutputPaths : IOutputPaths
{
    /// <inheritdoc />
    public bool TryCreateDirectory(string path)
    {
        if (Exists(path))
        {
            return true;
        }

        try
        {
            Directory.CreateDirectory(path);

            return true;
        }
        catch (Exception exception) when (IsCreateFailure(exception))
        {
            return false;
        }
    }

    /// <inheritdoc />
    public bool Exists(string path) => File.Exists(path) || Directory.Exists(path);

    /// <inheritdoc />
    public bool TryDeleteFile(string path)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            File.Delete(path);

            return true;
        }
        catch (Exception exception) when (IsDeleteFailure(exception))
        {
            return false;
        }
    }

    /// <summary>
    /// Tells whether <paramref name="exception" /> is one <see cref="File.Delete(string)" /> raises
    /// for a file the operating system will not delete, such as one another process holds open.
    /// </summary>
    /// <param name="exception">The exception the delete threw.</param>
    /// <returns>
    /// <see langword="true" /> for an <see cref="IOException" /> or an
    /// <see cref="UnauthorizedAccessException" />.
    /// </returns>
    internal static bool IsDeleteFailure(Exception exception) =>
        exception is IOException or UnauthorizedAccessException;

    /// <summary>
    /// Tells whether <paramref name="exception" /> is one <see cref="Directory.CreateDirectory(string)" />
    /// raises for a directory the operating system will not create.
    /// </summary>
    /// <param name="exception">The exception the create threw.</param>
    /// <returns>
    /// <see langword="true" /> for an <see cref="IOException" />, an
    /// <see cref="UnauthorizedAccessException" />, an <see cref="ArgumentException" /> or a
    /// <see cref="NotSupportedException" />.
    /// </returns>
    internal static bool IsCreateFailure(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException;
}
