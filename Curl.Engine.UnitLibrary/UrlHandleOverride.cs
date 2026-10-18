using Curl.Api.Codes;
using Curl.Api.EngineContracts.Url;
using Curl.Api.Options;

namespace Curl.Engine;

/// <summary>
/// <c>CURLOPT_CURLU</c> (BL-2628, ADR-0673): a set <see cref="Curl.Api.Options.Network.EasyNetworkOptions.UrlHandle" />
/// read whole when a perform starts, in place of <c>CURLOPT_URL</c>, as curl 8.22.0's <c>Curl_pretransfer</c> in
/// <c>lib/transfer.c</c> replaces <c>STRING_SET_URL</c> with <c>curl_url_get(data->set.uh, CURLUPART_URL)</c>; a
/// handle whose URL cannot be read leaves no URL, so the perform answers <c>No URL set</c> as libcurl does. The path
/// goes in as <c>parseurlandfillconn</c> in <c>lib/url.c</c> reads it, with <c>CURLU_URLENCODE</c>, so a space a
/// <c>CURLU_ALLOW_SPACE</c> parse kept is sent as <c>%20</c> (upstream test1543, BL-2643).
/// </summary>
internal static class UrlHandleOverride
{
    /// <summary>
    /// <paramref name="options" /> with <c>CURLOPT_URL</c> replaced by the URL handle's whole URL, its path
    /// URL-encoded; as they are when no handle is set.
    /// </summary>
    /// <param name="options">The handle's options.</param>
    /// <returns>The options the perform runs with.</returns>
    internal static EasyOptions Apply(EasyOptions options)
    {
        if (options.Network.UrlHandle is not { } handle)
        {
            return options;
        }

        using ICurlUrlHandle encoded = handle.Duplicate();
        _ = encoded.Get(CurlUrlPart.Path, CurlUrlFlags.UrlEncode, out string? path);
        _ = encoded.Set(CurlUrlPart.Path, path, CurlUrlFlags.None);
        string? url = encoded.Get(CurlUrlPart.Url, CurlUrlFlags.None, out string? whole) == CurlUrlCode.Ok ? whole : null;
        return options with { Network = options.Network with { Url = url } };
    }
}
