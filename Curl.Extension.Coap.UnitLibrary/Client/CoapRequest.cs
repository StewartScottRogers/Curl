using System.Net;
using System.Text;
using Curl.Extension.Coap.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Coap.Client;

/// <summary>Builds the request a transfer sends: its method, its URI options (RFC 7252 section 6.4) and its payload.</summary>
internal static class CoapRequest
{
    /// <summary>The default port of the <c>coap</c> scheme (RFC 7252 section 6.1).</summary>
    public const int DefaultPort = 5683;

    /// <summary>The default port of the <c>coaps</c> scheme (RFC 7252 section 6.2).</summary>
    public const int SecureDefaultPort = 5684;

    /// <summary>The scheme of CoAP over DTLS (RFC 7252 section 6.2).</summary>
    public const string SecureScheme = "coaps";

    private static readonly Dictionary<string, CoapCode> Methods = new(StringComparer.Ordinal)
    {
        ["GET"] = CoapCode.Get,
        ["POST"] = CoapCode.Post,
        ["PUT"] = CoapCode.Put,
        ["DELETE"] = CoapCode.Delete,
    };

    /// <summary>Gives the UDP port a URL names: its port, else 5684 for <c>coaps</c> and 5683 for <c>coap</c>.</summary>
    /// <param name="url">The URL.</param>
    /// <returns>The port.</returns>
    public static int PortOf(CurlUrl url) => url.Port >= 0 ? url.Port : IsSecure(url) ? SecureDefaultPort : DefaultPort;

    /// <summary>Tells whether a URL is <c>coaps</c>, CoAP over DTLS.</summary>
    /// <param name="url">The URL.</param>
    /// <returns><see langword="true" /> for a <c>coaps</c> URL.</returns>
    public static bool IsSecure(CurlUrl url) => string.Equals(url.Scheme, SecureScheme, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gives the request method: <c>-X</c>'s method when given, else POST with <c>-d</c>, else GET.
    /// </summary>
    /// <param name="context">The transfer.</param>
    /// <returns>The method code.</returns>
    /// <exception cref="CoapException"><c>-X</c> names a method RFC 7252 does not define (exit 2).</exception>
    public static CoapCode MethodOf(ITransferContext context)
    {
        string name = context.Http?.CustomMethod ?? DefaultMethodOf(context);
        return Methods.TryGetValue(name, out CoapCode method)
            ? method
            : throw new CoapException(CurlExitCode.FailedInit, $"CoAP has no method \"{name}\"; use GET, POST, PUT or DELETE");
    }

    /// <summary>Gives the request payload: the whole <c>-T</c> upload when given, else the <c>-d</c> data, else none.</summary>
    /// <param name="context">The transfer.</param>
    /// <returns>The payload.</returns>
    public static async ValueTask<byte[]> PayloadOfAsync(ITransferContext context)
    {
        if (context.Upload is not { } upload)
        {
            return context.PostData?.ToArray() ?? [];
        }

        var payload = new MemoryStream();
        await upload.CopyToAsync(payload, context.CancellationToken).ConfigureAwait(false);
        return payload.ToArray();
    }

    /// <summary>
    /// Gives the Uri-Host, Uri-Path and Uri-Query options for a URL (RFC 7252 section 6.4): Uri-Host
    /// with the host in lowercase unless it is an IP literal, one Uri-Path per path segment and one
    /// Uri-Query per <c>&amp;</c>-separated query argument, each percent-decoded. Uri-Port is never
    /// sent, because the request goes to the URL's port.
    /// </summary>
    /// <param name="url">The URL.</param>
    /// <returns>The options, in ascending number.</returns>
    public static IReadOnlyList<CoapOption> UriOptionsOf(CurlUrl url) =>
        [.. HostOptionsOf(url), .. PathOptionsOf(url.AbsolutePath), .. QueryOptionsOf(url.Query)];

    /// <summary>Gives the bytes a URL component stands for: its UTF-8 with each valid <c>%XX</c> as the byte it names.</summary>
    /// <param name="component">The component as written.</param>
    /// <returns>The decoded bytes.</returns>
    internal static byte[] PercentDecoded(string component)
    {
        byte[] encoded = Encoding.UTF8.GetBytes(component);
        var decoded = new List<byte>(encoded.Length);
        for (int index = 0; index < encoded.Length; index++)
        {
            if (encoded[index] == (byte)'%' && index + 2 < encoded.Length && IsHexPair(encoded[index + 1], encoded[index + 2]))
            {
                decoded.Add(Convert.FromHexString([(char)encoded[index + 1], (char)encoded[index + 2]])[0]);
                index += 2;
            }
            else
            {
                decoded.Add(encoded[index]);
            }
        }

        return [.. decoded];
    }

    private static string DefaultMethodOf(ITransferContext context)
    {
        if (context.Upload is not null)
        {
            return "PUT";
        }

        return context.PostData is not null || context.PostWithoutData ? "POST" : "GET";
    }

    private static IEnumerable<CoapOption> HostOptionsOf(CurlUrl url) =>
        IPAddress.TryParse(url.Host.Trim('[', ']'), out _)
            ? []
            : [new CoapOption(CoapOptionNumber.UriHost, Encoding.UTF8.GetBytes(url.IdnHost.ToLowerInvariant()))];

    private static IEnumerable<CoapOption> PathOptionsOf(string path) =>
        path is "" or "/"
            ? []
            : path[1..].Split('/').Select(segment => new CoapOption(CoapOptionNumber.UriPath, PercentDecoded(segment)));

    private static IEnumerable<CoapOption> QueryOptionsOf(string? query) =>
        string.IsNullOrEmpty(query)
            ? []
            : query.Split('&').Select(argument => new CoapOption(CoapOptionNumber.UriQuery, PercentDecoded(argument)));

    private static bool IsHexPair(byte high, byte low) => Uri.IsHexDigit((char)high) && Uri.IsHexDigit((char)low);
}
