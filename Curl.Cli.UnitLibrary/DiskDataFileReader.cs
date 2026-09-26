namespace Curl.Cli;

/// <summary>
/// The <see cref="IDataFileReader"/> backed by the disk and this process's standard input. A file
/// that cannot be opened or read (missing, a directory, access denied, an empty or malformed path)
/// is reported as unread rather than thrown.
/// </summary>
/// <param name="readAllBytes">Reads every byte of a file, throwing as <see cref="File.ReadAllBytes(string)"/> does.</param>
/// <param name="openStandardInput">Opens standard input for reading.</param>
public sealed class DiskDataFileReader(Func<string, byte[]> readAllBytes, Func<Stream> openStandardInput) : IDataFileReader
{
    /// <summary>The reader over the disk, through <see cref="File.ReadAllBytes(string)"/>, and <see cref="Console.OpenStandardInput()"/>.</summary>
    public static DiskDataFileReader ForProcess { get; } = new(File.ReadAllBytes, Console.OpenStandardInput);

    /// <inheritdoc/>
    public bool TryReadFile(string path, out byte[] contents)
    {
        try
        {
            contents = readAllBytes(path);
            return true;
        }
        catch (Exception exception) when (IsUnreadableFile(exception))
        {
            contents = [];
            return false;
        }
    }

    /// <inheritdoc/>
    public byte[] ReadStandardInput()
    {
        using Stream standardInput = openStandardInput();
        using MemoryStream contents = new();
        standardInput.CopyTo(contents);
        return contents.ToArray();
    }

    private static bool IsUnreadableFile(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException;
}
