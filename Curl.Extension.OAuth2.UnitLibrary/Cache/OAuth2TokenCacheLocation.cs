namespace Curl.Extension.OAuth2.Cache;

/// <summary>
/// Finds the directory the token cache file lives in by default: the first of the environment variables curl searches
/// for its own per-user <c>.curlrc</c> that is set and not empty (ADR-1034).
/// </summary>
public static class OAuth2TokenCacheLocation
{
    /// <summary>The environment variables searched, in order.</summary>
    public static readonly IReadOnlyList<string> SearchedVariables = ["CURL_HOME", "XDG_CONFIG_HOME", "HOME", "APPDATA", "USERPROFILE"];

    /// <summary>Finds the default directory.</summary>
    /// <param name="readEnvironmentVariable">Reads an environment variable, giving <see langword="null"/> when it is not set.</param>
    /// <returns>The directory, or <see langword="null"/> when none of the variables is set.</returns>
    public static string? FindDefaultDirectory(Func<string, string?> readEnvironmentVariable)
    {
        ArgumentNullException.ThrowIfNull(readEnvironmentVariable);
        foreach (var name in SearchedVariables)
        {
            var value = readEnvironmentVariable(name);
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }
        }

        return null;
    }
}
