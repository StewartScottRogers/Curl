using Curl.Extension.Abstractions;

namespace Curl.Extension.ObliviousDoh;

/// <summary>
/// The proxy and the target of Oblivious DoH (RFC 9230 section 4): the proxy relays each encrypted query to the
/// target, which alone can read it.
/// </summary>
/// <param name="Proxy">The proxy's URL, plain or ending in the URI template <c>{?targethost,targetpath}</c>.</param>
/// <param name="Target">The target's DoH URL, such as <c>https://odoh.example/dns-query</c>.</param>
public sealed record ObliviousDohEndpoints(Uri Proxy, Uri Target)
{
    private const string TargetTemplate = "{?targethost,targetpath}";

    /// <summary>
    /// Gets the URL each query is POSTed to: the proxy's, with the <c>targethost</c> and <c>targetpath</c> query
    /// variables section 4.1 requires, as in its example <c>?targethost=dnstarget.example&amp;targetpath=/dns-query</c>.
    /// </summary>
    public Uri ProxyRequestUri
    {
        get
        {
            string proxy = Proxy.OriginalString.Replace(TargetTemplate, string.Empty, StringComparison.Ordinal);
            string separator = proxy.Contains('?', StringComparison.Ordinal) ? "&" : "?";
            return new Uri($"{proxy}{separator}targethost={Escape(Target.Authority)}&targetpath={Escape(Target.AbsolutePath)}");
        }
    }

    /// <summary>
    /// Gets the URL the target's <c>ObliviousDoHConfigs</c> are fetched from, <c>/.well-known/odohconfigs</c> on the
    /// target's origin; RFC 9230 section 5 leaves discovery out of scope (ADR-1127).
    /// </summary>
    public Uri ConfigsUri => new(Target, "/.well-known/odohconfigs");

    /// <summary>Reads the two option values.</summary>
    /// <param name="proxy">The <c>--odoh-proxy</c> value.</param>
    /// <param name="target">The <c>--odoh-target</c> value.</param>
    /// <returns>The endpoints.</returns>
    /// <exception cref="ArgumentException">A value is not an absolute <c>http</c> or <c>https</c> URL.</exception>
    public static ObliviousDohEndpoints Parse(string proxy, string target) =>
        new(UrlOf(proxy, ObliviousDohExtension.ProxyOption), UrlOf(target, ObliviousDohExtension.TargetOption));

    private static Uri UrlOf(string value, ExtensionOption option) =>
        Uri.TryCreate(value, UriKind.Absolute, out Uri? url) && (url.Scheme == Uri.UriSchemeHttps || url.Scheme == Uri.UriSchemeHttp)
            ? url
            : throw new ArgumentException($"--{option.Name} needs an http or https URL, not '{value}'", nameof(value));

    // Percent-encodes a query value, leaving '/' as section 4.1's example does.
    private static string Escape(string value) => Uri.EscapeDataString(value).Replace("%2F", "/", StringComparison.Ordinal);
}
