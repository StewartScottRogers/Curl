namespace Curl.Extension.RecordReplay;

/// <summary>
/// A directory of exchange files (ADR-1135 section 1), one per request, named <c>&lt;key&gt;.exchange</c> by its
/// <see cref="ExchangeKey" />. A write goes to a temporary file first and replaces the file in one move, so a
/// reader sees the old exchange or the new one, never half of one.
/// </summary>
/// <param name="path">The directory; created on the first write.</param>
public sealed class ExchangeDirectory(string path)
{
    /// <summary>Gets the directory's path, as the option gave it.</summary>
    public string Path { get; } = path;

    /// <summary>Writes <paramref name="exchange" /> as the file for <paramref name="key" />, replacing any it had.</summary>
    public async Task WriteAsync(string key, RecordedExchange exchange, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path);
        var filePath = FilePath(key);
        var temporaryPath = filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        await File.WriteAllBytesAsync(temporaryPath, RecordedExchangeEncoding.Encode(exchange), cancellationToken);
        File.Move(temporaryPath, filePath, overwrite: true);
    }

    /// <summary>Reads the file for <paramref name="key" />; <see langword="null" /> when there is none or it is not an exchange file.</summary>
    public async Task<RecordedExchange?> ReadAsync(string key, CancellationToken cancellationToken)
    {
        byte[] bytes;
        try
        {
            bytes = await File.ReadAllBytesAsync(FilePath(key), cancellationToken);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }

        return RecordedExchangeEncoding.Decode(bytes);
    }

    private string FilePath(string key) => System.IO.Path.Combine(Path, key + ".exchange");
}
