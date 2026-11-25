namespace Curl.Extension.HttpCache;

/// <summary>
/// A directory of cache entry files, one stored response each, named by its <see cref="CacheKey"/>
/// with the extension <c>.entry</c>. A write goes to a temporary file first and replaces the entry
/// in one move, so a reader sees the old entry or the new one, never half of one.
/// </summary>
/// <param name="path">The directory; created on the first write.</param>
/// <exception cref="ArgumentException">The path cannot name a directory on this platform; the message names <c>--cache-dir</c> and the path and carries no operating system text (ADR-1289).</exception>
public sealed class CacheDirectory(string path)
{
    private readonly string _path = Validated(path);

    /// <summary>Writes <paramref name="response"/> as the entry for <paramref name="key"/>, replacing any entry it had.</summary>
    public async Task WriteAsync(string key, StoredResponse response, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_path);
        var entryPath = EntryPath(key);
        var temporaryPath = entryPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        await File.WriteAllBytesAsync(temporaryPath, StoredResponseEncoding.Encode(response), cancellationToken);
        File.Move(temporaryPath, entryPath, overwrite: true);
    }

    /// <summary>Reads the entry for <paramref name="key"/>; <see langword="null"/> when there is none or it is corrupt or truncated.</summary>
    public async Task<StoredResponse?> ReadAsync(string key, CancellationToken cancellationToken)
    {
        byte[] bytes;
        try
        {
            bytes = await File.ReadAllBytesAsync(EntryPath(key), cancellationToken);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }

        return StoredResponseEncoding.Decode(bytes);
    }

    private static string Validated(string path)
    {
        try
        {
            var full = Path.GetFullPath(path);
            var names = full[Path.GetPathRoot(full.AsSpan()).Length..].Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (names.All(name => name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0) && !File.Exists(full))
            {
                return path;
            }
        }
        catch (SystemException)
        {
            // Reported below as the same refusal.
        }

        throw new ArgumentException($"--cache-dir cannot use '{path}'");
    }

    private string EntryPath(string key) => Path.Combine(_path, key + ".entry");
}
