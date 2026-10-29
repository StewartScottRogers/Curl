namespace Curl.Extension.HttpCache;

/// <summary>
/// The directives of every <c>Cache-Control</c> field in a header list (RFC 9111 §5.2), by
/// case-insensitive name; the first occurrence of a directive wins. A quoted value loses its
/// quotes; a comma inside one splits it, as a list of field names is never needed here.
/// </summary>
public sealed class CacheControlDirectives
{
    private readonly Dictionary<string, string?> _directives;

    private CacheControlDirectives(Dictionary<string, string?> directives) => _directives = directives;

    /// <summary>Collects the directives of every <c>Cache-Control</c> field in <paramref name="headers"/>.</summary>
    public static CacheControlDirectives Parse(IReadOnlyList<KeyValuePair<string, string>> headers)
    {
        var directives = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in headers)
        {
            if (!string.Equals(header.Key, "Cache-Control", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var directive in header.Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                AddDirective(directives, directive);
            }
        }

        return new CacheControlDirectives(directives);
    }

    /// <summary>Whether the directive named <paramref name="name"/> is present, with or without a value.</summary>
    public bool Has(string name) => _directives.ContainsKey(name);

    /// <summary>Reads the directive named <paramref name="name"/> as delta-seconds; false when it is absent or its value is not delta-seconds.</summary>
    public bool TryGetDeltaSeconds(string name, out TimeSpan seconds)
    {
        seconds = TimeSpan.Zero;
        return _directives.TryGetValue(name, out var value) && DeltaSeconds.TryParse(value, out seconds);
    }

    private static void AddDirective(Dictionary<string, string?> directives, string directive)
    {
        var equals = directive.IndexOf('=');
        if (equals < 0)
        {
            directives.TryAdd(directive, null);
            return;
        }

        directives.TryAdd(directive[..equals].TrimEnd(), directive[(equals + 1)..].Trim().Trim('"'));
    }
}
