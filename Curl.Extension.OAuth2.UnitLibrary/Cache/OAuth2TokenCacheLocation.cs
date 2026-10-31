namespace Curl.Extension.OAuth2.Cache;

/// <summary>
/// Finds the directory the token cache file lives in by default: the first of the environment variables that is set and
/// not empty, searched in the order curl searches the directories of its own per-user <c>.curlrc</c> (ADR-1034):
/// <c>CURL_HOME</c>, <c>XDG_CONFIG_HOME</c>, <c>HOME</c>, then, on Windows only as in curl, <c>USERPROFILE</c> and <c>APPDATA</c>.
/// </summary>
public static class OAuth2TokenCacheLocation
{
    private const int WindowsOnlyFrom = 3;

    /// <summary>The environment variables searched, in order; the last two are searched on Windows only.</summary>
    public static readonly IReadOnlyList<string> SearchedVariables = ["CURL_HOME", "XDG_CONFIG_HOME", "HOME", "USERPROFILE", "APPDATA"];

    /// <summary>Finds the default directory.</summary>
    /// <param name="readEnvironmentVariable">Reads an environment variable, giving <see langword="null"/> when it is not set.</param>
    /// <param name="isWindows">Whether to search the Windows-only variables; <see langword="null"/> asks the running operating system.</param>
    /// <returns>The directory, or <see langword="null"/> when none of the variables is set.</returns>
    public static string? FindDefaultDirectory(Func<string, string?> readEnvironmentVariable, bool? isWindows = null)
    {
        ArgumentNullException.ThrowIfNull(readEnvironmentVariable);
        var count = (isWindows ?? OperatingSystem.IsWindows()) ? SearchedVariables.Count : WindowsOnlyFrom;
        for (var index = 0; index < count; index++)
        {
            var value = readEnvironmentVariable(SearchedVariables[index]);
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }
        }

        return null;
    }
}
