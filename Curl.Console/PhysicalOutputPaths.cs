namespace Curl.Console;

/// <summary>
/// <see cref="IOutputPaths" /> over the real disk.
/// </summary>
internal sealed class PhysicalOutputPaths : IOutputPaths
{
    /// <inheritdoc />
    public bool TryCreateDirectory(string path)
    {
        if (File.Exists(path) || Directory.Exists(path))
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
