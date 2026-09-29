using System.Diagnostics.CodeAnalysis;
using Curl.Protocol.Abstractions;

namespace Curl.Cli;

/// <summary>
/// The proxy a command line names: the <c>-x</c> / <c>--proxy</c>, <c>--proxy1.0</c>, <c>--socks4</c>, <c>--socks4a</c>,
/// <c>--socks5</c> or <c>--socks5-hostname</c> value as given, and the kind of proxy that option
/// means when the value has no scheme.
/// </summary>
/// <remarks>
/// Parsing the host, port and user information out of <see cref="Address"/> is the proxy selector's
/// job, as is reporting an address that is not a URL at all (curl 8.21.0 exits 5 for
/// <c>-x ://h:1</c>). This type only reads the scheme, because curl's choice of proxy kind hangs
/// on it. An empty <see cref="Address"/> (<c>-x ''</c>) asks for no proxy.
/// </remarks>
/// <param name="Address">The value as given on the command line, possibly empty.</param>
/// <param name="KindWithoutScheme">
/// The kind the option names: <see cref="ProxyKind.Http"/> for <c>-x</c>, a SOCKS kind for a
/// <c>--socks</c> option. A scheme in <paramref name="Address"/> outranks it.
/// </param>
public sealed record CommandLineProxy(string Address, ProxyKind KindWithoutScheme)
{
    private const string SchemeSeparator = "://";

    /// <summary>
    /// The schemes curl 8.21.0 accepts on a proxy, matched case-insensitively, and the kind each means.
    /// </summary>
    private static readonly (string Scheme, ProxyKind Kind)[] SupportedSchemes =
    [
        ("http", ProxyKind.Http),
        ("https", ProxyKind.Https),
        ("socks", ProxyKind.Socks4),
        ("socks4", ProxyKind.Socks4),
        ("socks4a", ProxyKind.Socks4a),
        ("socks5", ProxyKind.Socks5),
        ("socks5h", ProxyKind.Socks5Hostname),
    ];

    /// <summary>
    /// Reads the kind of proxy <see cref="Address"/> names: the kind of its scheme when it has one
    /// (the text before <c>://</c>), else <see cref="KindWithoutScheme"/>. A scheme curl 8.21.0 does
    /// not accept fails the transfer with exit 7 and <c>Unsupported proxy scheme for '&lt;address&gt;'</c>.
    /// </summary>
    /// <remarks>
    /// Measured with the reference curl 8.21.0 on 2026-09-26 (<c>Record-CurlExchange.ps1</c>, reading
    /// the first bytes the proxy received): <c>--socks5 A -x B</c> speaks HTTP to <c>B</c>;
    /// <c>-x A --socks5 B</c> and <c>--socks4 A -x socks5://B</c> speak SOCKS5; <c>--socks5 http://A</c>
    /// speaks HTTP; <c>-x socks://A</c> and <c>--socks4 A</c> speak SOCKS4; <c>HTTP://</c> and
    /// <c>SOCKS5H://</c> are accepted. <c>-x bogus://h:1</c>, <c>-x socks6://h:1</c>,
    /// <c>-x ftp://127.0.0.1:1</c> and <c>--socks5 bogus://h:1</c> exit 7 with
    /// <c>curl: (7) Unsupported proxy scheme for '&lt;value&gt;'</c>, after the command line is read.
    /// </remarks>
    /// <param name="kind">The kind of proxy, when the scheme is supported.</param>
    /// <param name="failure">The failed transfer, when it is not.</param>
    /// <returns><see langword="true"/> when the scheme is supported or absent.</returns>
    public bool TryGetKind(out ProxyKind kind, [NotNullWhen(false)] out TransferResult? failure)
    {
        failure = null;
        int separator = Address.IndexOf(SchemeSeparator, StringComparison.Ordinal);
        if (separator < 0)
        {
            kind = KindWithoutScheme;
            return true;
        }

        string scheme = Address[..separator];
        foreach ((string supported, ProxyKind supportedKind) in SupportedSchemes)
        {
            if (string.Equals(scheme, supported, StringComparison.OrdinalIgnoreCase))
            {
                kind = supportedKind == ProxyKind.Http ? HttpKindWithoutScheme() : supportedKind;
                return true;
            }
        }

        kind = default;
        failure = TransferResult.Failure(CurlExitCode.CouldntConnect, $"Unsupported proxy scheme for '{Address}'");
        return false;
    }

    /// <summary>
    /// The kind an <c>http://</c> scheme means: <see cref="ProxyKind.Http10"/> when the option was
    /// <c>--proxy1.0</c> (curl 8.21.0 sends <c>CONNECT … HTTP/1.0</c> for <c>--proxy1.0 http://A</c>,
    /// measured 2026-09-28), else <see cref="ProxyKind.Http"/>.
    /// </summary>
    private ProxyKind HttpKindWithoutScheme() =>
        KindWithoutScheme == ProxyKind.Http10 ? ProxyKind.Http10 : ProxyKind.Http;
}
