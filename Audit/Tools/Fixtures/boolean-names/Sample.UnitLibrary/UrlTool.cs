namespace Sample;

// Fixture for Find-BooleanNameMismatches.ps1 -SelfTest (BL-1370). Nothing here is real: two
// members the scan must flag and five it must leave alone (HasBody has no doc, which the compiler, not the scan, catches).
public static class UrlTool
{
    /// <summary>Tells whether <paramref name="url" /> starts with a scheme.</summary>
    /// <returns><see langword="true" /> when the URL names its own scheme.</returns>
    public static bool LacksScheme(string url) => url.Contains("://");

    /// <summary>Tells whether the URL starts with a scheme.</summary>
    /// <returns><see langword="true" /> when the URL names its own scheme.</returns>
    public static bool HasScheme(string url) => url.Contains("://");

    /// <returns><see langword="true" /> when the transfer has not received every byte yet.</returns>
    public static bool IsComplete(long received, long total) => received < total;

    /// <returns><see langword="true" /> when no byte of the body has arrived.</returns>
    public static bool NotStarted(long received) => received == 0;

    /// <returns><see langword="true" /> when the response carries no body.</returns>
    [System.Obsolete("fixture")]
    public static bool IsEmpty(long length) => length == 0;

    public static bool HasBody(long length) => length > 0;

    /// <returns><see langword="true" /> when the host can be reached.</returns>
    public static bool CanReach(string host) => host.Length > 0;

    /// <returns>Not a prefixed name, so never scanned.</returns>
    public static bool Matches(string url) => url.Length > 0;
}
