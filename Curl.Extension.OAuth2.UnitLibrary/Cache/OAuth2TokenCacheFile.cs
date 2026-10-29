using System.Runtime.Versioning;

namespace Curl.Extension.OAuth2.Cache;

/// <summary>
/// The token cache file, <see cref="FileName"/> in a directory the caller names (ADR-1034): read whole, a missing,
/// unreadable or corrupt file reading as empty, and written whole and atomically, to a temporary file beside it that
/// then replaces it, created owner-only (<c>0600</c>) where the operating system has Unix permissions.
/// </summary>
public sealed class OAuth2TokenCacheFile
{
    /// <summary>The cache file's name.</summary>
    public const string FileName = "curl-oauth2-tokens.json";

    private const UnixFileMode OwnerReadWrite = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    [UnsupportedOSPlatformGuard("windows")]
    private readonly bool setsOwnerOnlyMode;

    /// <summary>Initializes a new instance of the <see cref="OAuth2TokenCacheFile"/> class.</summary>
    /// <param name="directory">The directory the file lives in, created when missing.</param>
    public OAuth2TokenCacheFile(string directory)
        : this(directory, !OperatingSystem.IsWindows())
    {
    }

    /// <summary>Initializes a new instance of the <see cref="OAuth2TokenCacheFile"/> class.</summary>
    /// <param name="directory">The directory the file lives in, created when missing.</param>
    /// <param name="setsOwnerOnlyMode">
    /// Whether the file is created with <see cref="FileStreamOptions.UnixCreateMode"/>, which throws on Windows.
    /// </param>
    internal OAuth2TokenCacheFile(string directory, bool setsOwnerOnlyMode)
    {
        ArgumentException.ThrowIfNullOrEmpty(directory);
        Directory = directory;
        FilePath = Path.Combine(directory, FileName);
        this.setsOwnerOnlyMode = setsOwnerOnlyMode;
    }

    /// <summary>Gets the directory the file lives in.</summary>
    public string Directory { get; }

    /// <summary>Gets the file's path.</summary>
    public string FilePath { get; }

    /// <summary>Reads every cached token.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The cached tokens by key; none when the file is missing, unreadable or corrupt.</returns>
    public async Task<Dictionary<OAuth2TokenCacheKey, OAuth2CachedToken>> ReadAsync(CancellationToken cancellationToken)
    {
        string text;
        try
        {
            text = await File.ReadAllTextAsync(FilePath, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return [];
        }

        return OAuth2TokenCacheFormat.Read(text);
    }

    /// <summary>Replaces the file with <paramref name="entries"/>, atomically.</summary>
    /// <param name="entries">The cached tokens by key.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Whether the file was written; a failure leaves the old file as it was.</returns>
    public async Task<bool> WriteAsync(IReadOnlyDictionary<OAuth2TokenCacheKey, OAuth2CachedToken> entries, CancellationToken cancellationToken)
    {
        var bytes = OAuth2TokenCacheFormat.Write(entries);
        try
        {
            System.IO.Directory.CreateDirectory(Directory);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }

        var temporaryPath = $"{FilePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var stream = new FileStream(temporaryPath, CreateOptions()))
            {
                await stream.WriteAsync(bytes, cancellationToken);
            }

            File.Move(temporaryPath, FilePath, overwrite: true);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            File.Delete(temporaryPath);
            return false;
        }
    }

    private FileStreamOptions CreateOptions() =>
        setsOwnerOnlyMode ? OwnerOnlyCreateOptions() : new() { Mode = FileMode.CreateNew, Access = FileAccess.Write };

    [UnsupportedOSPlatform("windows")]
    private static FileStreamOptions OwnerOnlyCreateOptions() =>
        new() { Mode = FileMode.CreateNew, Access = FileAccess.Write, UnixCreateMode = OwnerReadWrite };
}
