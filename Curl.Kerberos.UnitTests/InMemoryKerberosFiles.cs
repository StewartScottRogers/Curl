namespace Curl.Kerberos;

/// <summary>An <see cref="IKerberosFileReader" /> over a dictionary of paths, so no test touches the disk.</summary>
internal sealed class InMemoryKerberosFiles : IKerberosFileReader
{
    private readonly Dictionary<string, byte[]> files = new(StringComparer.Ordinal);

    public List<string> PathsRead { get; } = [];

    public InMemoryKerberosFiles Add(string path, byte[] bytes)
    {
        files[path] = bytes;
        return this;
    }

    public byte[]? ReadAllBytes(string path)
    {
        PathsRead.Add(path);
        return files.GetValueOrDefault(path);
    }
}
