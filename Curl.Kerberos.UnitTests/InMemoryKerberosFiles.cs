using System.Text;

namespace Curl.Kerberos;

/// <summary>An <see cref="IKerberosFileReader" /> and <see cref="IKerberosFileWriter" /> over a dictionary of paths, so no test touches the disk.</summary>
internal sealed class InMemoryKerberosFiles : IKerberosFileReader, IKerberosFileWriter
{
    private readonly Dictionary<string, byte[]> files = new(StringComparer.Ordinal);

    private readonly Dictionary<string, string[]> directories = new(StringComparer.Ordinal);

    public List<string> PathsRead { get; } = [];

    public InMemoryKerberosFiles Add(string path, byte[] bytes)
    {
        files[path] = bytes;
        return this;
    }

    public InMemoryKerberosFiles Add(string path, string text) => Add(path, Encoding.UTF8.GetBytes(text));

    public InMemoryKerberosFiles AddDirectory(string path, params string[] fileNames)
    {
        directories[path] = fileNames;
        return this;
    }

    /// <summary>
    /// Gets a value indicating whether each read returns new bytes, as a disk does, so a reader
    /// that zeroes what it read leaves the file intact; otherwise a read returns the file's own
    /// array, so a test sees the zeroing.
    /// </summary>
    public bool ReturnsCopies { get; init; }

    public byte[]? ReadAllBytes(string path)
    {
        PathsRead.Add(path);
        byte[]? bytes = files.GetValueOrDefault(path);
        return ReturnsCopies && bytes is not null ? [.. bytes] : bytes;
    }

    public IReadOnlyList<string>? ListFileNames(string path) => directories.GetValueOrDefault(path);

    public bool AppendAllBytes(string path, ReadOnlySpan<byte> bytes)
    {
        if (!files.TryGetValue(path, out byte[]? existing))
        {
            return false;
        }

        files[path] = [.. existing, .. bytes];
        return true;
    }

    public byte[] Contents(string path) => [.. files[path]];
}
