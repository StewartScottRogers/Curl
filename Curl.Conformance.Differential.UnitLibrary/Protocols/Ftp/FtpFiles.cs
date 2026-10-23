namespace Curl.Conformance.Differential.Protocols.Ftp;

/// <summary>proto_servers.py's <c>FILES</c>: the files the FTP responder serves, by absolute path.</summary>
public static class FtpFiles
{
    /// <summary>The names the LIST and NLST listings show, in order.</summary>
    public static readonly string[] ListedNames = ["a.txt", "dir", "big.bin"];

    private static readonly Dictionary<string, byte[]> Files = new(StringComparer.Ordinal)
    {
        ["/a.txt"] = "alpha file\n"u8.ToArray(),
        ["/dir/b.txt"] = "bravo\n"u8.ToArray(),
        ["/big.bin"] = [.. Enumerable.Repeat(Enumerable.Range(0, 256).Select(value => (byte)value), 400).SelectMany(chunk => chunk)],
    };

    /// <summary>Finds the file at <paramref name="path"/>.</summary>
    public static bool TryGet(string path, out byte[] content) => Files.TryGetValue(path, out content!);

    /// <summary>Gets the size the listing shows for <paramref name="name"/>: the file's length, or 2 (<c>b"xx"</c>) for a name that is no file.</summary>
    public static int ListedSize(string name) => TryGet("/" + name, out var content) ? content.Length : 2;
}
