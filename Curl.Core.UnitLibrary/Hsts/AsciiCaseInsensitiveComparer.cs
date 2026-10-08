namespace Curl.Core.Hsts;

/// <summary>
/// Compares host names as <see cref="AsciiText.EqualsIgnoringCase" /> does, so a dictionary keyed
/// by host name finds exactly the entries libcurl's <c>curl_strnequal</c> would match.
/// </summary>
internal sealed class AsciiCaseInsensitiveComparer : IEqualityComparer<string>
{
    /// <summary>The one instance; the comparer holds no state.</summary>
    public static readonly AsciiCaseInsensitiveComparer Instance = new();

    private AsciiCaseInsensitiveComparer()
    {
    }

    /// <inheritdoc />
    public bool Equals(string? x, string? y) => AsciiText.EqualsIgnoringCase(x, y);

    /// <inheritdoc />
    public int GetHashCode(string obj) => AsciiText.GetHashCodeIgnoringCase(obj);
}
