using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Core;

/// <summary>
/// Rewrites an <c>ipfs://&lt;cid&gt;/&lt;path&gt;</c> or <c>ipns://&lt;name&gt;/&lt;path&gt;</c>
/// URL to the HTTP gateway URL curl 8.21.0 fetches instead, reading the environment and the
/// gateway file through injected functions so no test touches the real ones.
/// </summary>
/// <remarks>
/// <para>
/// The gateway is <c>--ipfs-gateway</c> when given, a URL with no scheme getting the one
/// <see cref="UrlSchemeGuesser" /> guesses (curl's <c>CURLU_GUESS_SCHEME</c>); else the
/// <c>IPFS_GATEWAY</c> environment variable, even empty; else the first line of <c>$IPFS_PATH/gateway</c>, or of <c>$HOME/.ipfs/gateway</c> when
/// <c>IPFS_PATH</c> is not set. A gateway from the environment or the file must name its
/// scheme. <c>HOME</c> is the only home curl reads, on Windows too: <c>USERPROFILE</c> is
/// ignored. No gateway, or a file whose first line (up to <c>\r</c> or <c>\n</c>) is empty,
/// is <see cref="IpfsGatewayFailure.GatewayDetectionFailed" />.
/// </para>
/// <para>
/// The result has the gateway's scheme, host and port, the port always written (a gateway
/// with none gets its scheme's default), and the input's user information as written,
/// even an empty user or password (<c>u:@</c>, <c>@</c>), query and fragment; an empty
/// query or fragment is dropped. The path is the gateway's, a
/// <c>/</c>, the input's scheme in lower case, a <c>/</c>, the cid or name, then the
/// input's path unless that is only <c>/</c>. Each part is percent-decoded and the whole
/// path encoded again: a space, <c>" # % &lt; &gt; ? \ ^ `</c>, <c>|</c> and every byte from
/// 0x7F up become upper-case <c>%XX</c>, and a decoded byte below 0x20 makes the URL
/// <see cref="IpfsGatewayFailure.MalformedTargetUrl" />, as does a gateway with a query, an
/// IPv6 host, no host or a scheme curl does not know. The gateway's user information and
/// fragment are ignored. Measured against curl 8.21.0 on 2026-09-27 (BL-210).
/// </para>
/// </remarks>
/// <param name="readEnvironmentVariable">
/// Returns the value of the named environment variable, or <see langword="null" /> when it
/// is not set; production passes <see cref="Environment.GetEnvironmentVariable(string)" />.
/// </param>
/// <param name="readFileText">
/// Returns the whole text of the file at the given path, or <see langword="null" /> when it
/// cannot be read.
/// </param>
public sealed class IpfsGatewayRewriter(
    Func<string, string?> readEnvironmentVariable,
    Func<string, string?> readFileText)
{
    /// <summary>The environment variable naming the gateway.</summary>
    public const string GatewayVariableName = "IPFS_GATEWAY";

    /// <summary>The environment variable naming the IPFS data folder that holds the gateway file.</summary>
    public const string IpfsPathVariableName = "IPFS_PATH";

    /// <summary>The environment variable naming the home folder whose <c>.ipfs</c> folder is the default IPFS data folder.</summary>
    public const string HomeVariableName = "HOME";

    private const string EncodedPathBytes = " \"#%<>?\\^`|";

    private readonly Func<string, string?> readEnvironmentVariable =
        readEnvironmentVariable ?? throw new ArgumentNullException(nameof(readEnvironmentVariable));

    private readonly Func<string, string?> readFileText =
        readFileText ?? throw new ArgumentNullException(nameof(readFileText));

    /// <summary>Tells whether <paramref name="url" /> is one this class rewrites: <c>ipfs</c> or <c>ipns</c>.</summary>
    /// <param name="url">The parsed URL of the transfer.</param>
    /// <returns><see langword="true" /> for an <c>ipfs</c> or <c>ipns</c> URL.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="url" /> is <see langword="null" />.</exception>
    public static bool IsIpfsUrl(CurlUrl url)
    {
        ArgumentNullException.ThrowIfNull(url);

        return url.Scheme is "ipfs" or "ipns";
    }

    /// <summary>Rewrites <paramref name="url" /> to its gateway URL.</summary>
    /// <param name="url">The parsed <c>ipfs</c> or <c>ipns</c> URL of the transfer.</param>
    /// <param name="gatewayOption">The <c>--ipfs-gateway</c> text; <see langword="null" /> when not given.</param>
    /// <param name="gatewayUrl">The gateway URL to fetch; <see langword="null" /> on failure.</param>
    /// <param name="failure">Why the URL could not be rewritten; <see langword="null" /> on success.</param>
    /// <returns><see langword="true" /> when the URL was rewritten.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="url" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException"><paramref name="url" /> is not an <c>ipfs</c> or <c>ipns</c> URL.</exception>
    public bool TryRewrite(
        CurlUrl url,
        string? gatewayOption,
        [NotNullWhen(true)] out string? gatewayUrl,
        [NotNullWhen(false)] out IpfsGatewayFailure? failure)
    {
        if (!IsIpfsUrl(url))
        {
            throw new ArgumentException($"\"{url.OriginalString}\" is not an ipfs or ipns URL.", nameof(url));
        }

        gatewayUrl = null;
        string? gatewayText = gatewayOption is null ? ReadConfiguredGateway() : UrlSchemeGuesser.AddGuessedScheme(gatewayOption);
        if (gatewayText is null)
        {
            failure = IpfsGatewayFailure.GatewayDetectionFailed;
            return false;
        }

        failure = IpfsGatewayFailure.MalformedTargetUrl;
        if (!TryParseGateway(gatewayText, out CurlUrl? gateway) || !TryBuildPath(gateway, url, out string? path))
        {
            return false;
        }

        failure = null;
        gatewayUrl = gateway.Scheme + "://" + UserInformation(url) + gateway.Host
            + ":" + gateway.Port.ToString(CultureInfo.InvariantCulture) + path
            + Suffix('?', url.Query) + Suffix('#', url.Fragment);
        return true;
    }

    private static bool TryParseGateway(string gatewayText, [NotNullWhen(true)] out CurlUrl? gateway)
    {
        if (UrlSchemeGuesser.HasScheme(gatewayText)
            && CurlUrl.TryParse(gatewayText, pathAsIs: false, out gateway)
            && IsUsableGateway(gateway))
        {
            return true;
        }

        gateway = null;
        return false;
    }

    private static bool IsUsableGateway(CurlUrl gateway) =>
        string.IsNullOrEmpty(gateway.Query)
        && gateway.Host.Length > 0
        && !gateway.Host.StartsWith('[')
        && IsKnownScheme(gateway.Scheme);

    /// <summary>
    /// Tells whether curl knows <paramref name="scheme" />: whether a URL with it and no port
    /// gets a default port. The scheme came from a parsed URL, so the probe always parses.
    /// </summary>
    private static bool IsKnownScheme(string scheme) =>
        CurlUrl.Parse(scheme + "://host").Port != -1;

    private static bool TryBuildPath(CurlUrl gateway, CurlUrl url, [NotNullWhen(true)] out string? path)
    {
        string gatewayPath = gateway.AbsolutePath.EndsWith('/') ? gateway.AbsolutePath : gateway.AbsolutePath + "/";
        string inputPath = url.AbsolutePath == "/" ? string.Empty : url.AbsolutePath;
        List<byte> decoded = [];
        PercentDecode(gatewayPath + url.Scheme + "/", decoded);
        decoded.AddRange(Encoding.UTF8.GetBytes(url.Host));
        PercentDecode(inputPath, decoded);

        return TryPercentEncodePath(decoded, out path);
    }

    private static void PercentDecode(string text, List<byte> bytes)
    {
        int index = 0;
        while (index < text.Length)
        {
            if (text[index] == '%'
                && index + 2 < text.Length
                && byte.TryParse(text.AsSpan(index + 1, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out byte value))
            {
                bytes.Add(value);
                index += 3;
            }
            else
            {
                int length = char.IsSurrogatePair(text, index) ? 2 : 1;
                bytes.AddRange(Encoding.UTF8.GetBytes(text.Substring(index, length)));
                index += length;
            }
        }
    }

    private static bool TryPercentEncodePath(List<byte> bytes, [NotNullWhen(true)] out string? path)
    {
        path = null;
        StringBuilder builder = new(bytes.Count);
        foreach (byte value in bytes)
        {
            if (value < 0x20)
            {
                return false;
            }

            if (value >= 0x7F || EncodedPathBytes.Contains((char)value, StringComparison.Ordinal))
            {
                builder.Append('%').Append(value.ToString("X2", CultureInfo.InvariantCulture));
            }
            else
            {
                builder.Append((char)value);
            }
        }

        path = builder.ToString();
        return true;
    }

    private static string UserInformation(CurlUrl url) =>
        url.User is null ? string.Empty : url.User + (url.Password is null ? string.Empty : ":" + url.Password) + "@";

    private static string Suffix(char separator, string? text) =>
        string.IsNullOrEmpty(text) ? string.Empty : separator + text;

    private string? ReadConfiguredGateway() =>
        readEnvironmentVariable(GatewayVariableName) ?? ReadGatewayFile();

    private string? ReadGatewayFile()
    {
        string? folder = readEnvironmentVariable(IpfsPathVariableName) ?? HomeIpfsFolder();
        string? text = folder is null ? null : readFileText(folder.TrimEnd('/') + "/gateway");
        string firstLine = text is null ? string.Empty : text[..IndexOfLineEnd(text)];

        return firstLine.Length == 0 ? null : firstLine;
    }

    private string? HomeIpfsFolder()
    {
        string? home = readEnvironmentVariable(HomeVariableName);

        return string.IsNullOrEmpty(home) ? null : home.TrimEnd('/') + "/.ipfs";
    }

    private static int IndexOfLineEnd(string text)
    {
        int index = text.AsSpan().IndexOfAny('\r', '\n');

        return index < 0 ? text.Length : index;
    }
}
