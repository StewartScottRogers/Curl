using Curl.Api.Codes;
using Curl.Api.EngineContracts.Url;
using Curl.Api.Options;

namespace Curl.Engine;

/// <summary>
/// <c>CURLOPT_PORT</c> (BL-2606, ADR-0672): a non-zero <see cref="Curl.Api.Options.Network.EasyNetworkOptions.Port" />
/// written into the URL a perform transfers, as curl 8.22.0's <c>parse_remote_port</c> in <c>lib/url.c</c> sets
/// <c>CURLUPART_PORT</c> on the transfer's URL handle, so the connect, the request target (a proxy's absolute one
/// included), the <c>Host</c> header and the effective URL all carry it in place of the URL's own port or its scheme's default.
/// </summary>
internal static class UrlPortOverride
{
    /// <summary>
    /// <paramref name="options" /> with <c>CURLOPT_PORT</c> written into <c>CURLOPT_URL</c> through a
    /// <see cref="ManagedCurlUrlHandle" />; as they are when no port or no URL is set, or when the URL does not parse,
    /// leaving that to the perform's own malformed-URL answer.
    /// </summary>
    /// <param name="options">The handle's options.</param>
    /// <returns>The options the perform runs with.</returns>
    internal static EasyOptions Apply(EasyOptions options) =>
        options.Network is { Port: > 0, Url: { } url } network && UrlWithPort(url, network.Port, network.PathAsIs) is { } ported
            ? options with { Network = network with { Url = ported } }
            : options;

    /// <summary><paramref name="url" /> with its port set to <paramref name="port" />, or <see langword="null" /> when it does not parse.</summary>
    /// <param name="url">The URL set.</param>
    /// <param name="port">The port, 1 to 65535.</param>
    /// <param name="pathAsIs">Whether <c>CURLOPT_PATH_AS_IS</c> keeps the path's dot segments.</param>
    /// <returns>The URL with the port, or <see langword="null" />.</returns>
    private static string? UrlWithPort(string url, int port, bool pathAsIs)
    {
        using ManagedCurlUrlHandle handle = new();
        CurlUrlFlags flags = CurlUrlFlags.GuessScheme | CurlUrlFlags.NonSupportScheme | (pathAsIs ? CurlUrlFlags.PathAsIs : CurlUrlFlags.None);
        if (handle.Set(CurlUrlPart.Url, url, flags) != CurlUrlCode.Ok)
        {
            return null;
        }

        handle.Set(CurlUrlPart.Port, port.ToString(System.Globalization.CultureInfo.InvariantCulture), CurlUrlFlags.None);
        handle.Get(CurlUrlPart.Url, CurlUrlFlags.None, out string? ported);
        return ported;
    }
}
