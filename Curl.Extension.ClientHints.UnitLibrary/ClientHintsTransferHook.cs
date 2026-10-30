using System.Net;
using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.ClientHints;

/// <summary>
/// The <c>--client-hints</c> transfer hook (ADR-1102). It adds the WICG User-Agent Client Hints spec's low-entropy
/// hints (§3.1 "low-entropy hint table"), each a Structured Field (RFC 8941) as §3.3 defines them: <c>Sec-CH-UA</c>
/// (§3.3.1, a List of brands with a GREASE brand, §3.2 "grease"), <c>Sec-CH-UA-Mobile</c> (§3.3.6, a Boolean) and
/// <c>Sec-CH-UA-Platform</c> (§3.3.8, a String). They are sent only to a potentially trustworthy URL (§2, secure
/// contexts; RFC 8942 §4.1): <c>https</c>, <c>wss</c>, or a loopback host. A field <c>-H</c> already gave is left to it.
/// </summary>
/// <param name="platform">The <c>Sec-CH-UA-Platform</c> name, from <see cref="ClientHintsPlatform" />.</param>
internal sealed class ClientHintsTransferHook(string platform) : ITransferHook
{
    /// <summary>The brand list: curl's own brand, at the major version of curl 8.22.0 Curl presents, and a GREASE brand.</summary>
    internal const string Brands = "\"curl\";v=\"8\", \"Not_A Brand\";v=\"99\"";

    public IEnumerable<string> RequestHeadersFor(ITransferContext context)
    {
        if (!IsPotentiallyTrustworthy(context.Url))
        {
            return [];
        }

        KeyValuePair<string, string>[] hints =
        [
            new("Sec-CH-UA", Brands),
            new("Sec-CH-UA-Mobile", "?0"),
            new("Sec-CH-UA-Platform", "\"" + platform + "\""),
        ];
        var given = context.Http?.Headers ?? [];
        return hints.Where(hint => !GivesField(given, hint.Key)).Select(hint => hint.Key + ": " + hint.Value);
    }

    private static bool IsPotentiallyTrustworthy(CurlUrl url) =>
        url.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase)
        || url.Scheme.Equals("wss", StringComparison.OrdinalIgnoreCase)
        || IsLoopback(url.Host);

    private static bool IsLoopback(string host) =>
        host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
        || (IPAddress.TryParse(host.Trim('[', ']'), out var address) && IPAddress.IsLoopback(address));

    private static bool GivesField(IEnumerable<string> headers, string name) =>
        headers.Any(header => header.StartsWith(name + ":", StringComparison.OrdinalIgnoreCase));
}
