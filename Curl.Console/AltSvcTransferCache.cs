using System.Text;
using Curl.Cli;
using Curl.Core.AltSvc;
using Curl.Networking;
using Curl.Protocol.Abstractions;
using AltSvcAlternative = Curl.Protocol.Abstractions.AltSvcAlternative;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Console;

/// <summary>
/// One <c>http</c> or <c>https</c> transfer's alt-svc cache (<c>--alt-svc</c>), as curl 8.21.0 gives each
/// transfer's handle its own: the file read before the transfer, the <see cref="AltSvcRoute" /> and HTTP
/// version each connection of the transfer uses, the <c>Alt-Svc</c> headers it learns from, and the file
/// written when it ends.
/// </summary>
/// <remarks>
/// <para>
/// The lookup is libcurl 8.21.0's <c>url_set_conn_peer</c> (ADR-0226, BL-733 Notes), as curl.se's build with
/// HTTP/2 and HTTP/3 runs it on every platform. The version option names the origin versions an entry is
/// looked up under, in order, and the destination versions it may name:
/// </para>
/// <list type="table">
/// <listheader><term>Option</term><description>Looked up under; may switch to</description></listheader>
/// <item><term>none</term><description><c>h2</c>, <c>h1</c>; <c>h1</c>, <c>h2</c>, <c>h3</c></description></item>
/// <item><term><c>--http1.1</c></term><description><c>h1</c>; <c>h1</c></description></item>
/// <item><term><c>--http2</c></term><description><c>h2</c>, <c>h1</c>; <c>h1</c>, <c>h2</c></description></item>
/// <item><term><c>--http3</c></term><description><c>h3</c>, <c>h2</c>, <c>h1</c>; <c>h1</c>, <c>h2</c>, <c>h3</c></description></item>
/// <item><term><c>--http3-only</c></term><description><c>h3</c>; <c>h3</c></description></item>
/// <item><term><c>-0</c>, <c>--http2-prior-knowledge</c></term><description>nothing: no alternative is used</description></item>
/// </list>
/// <para>
/// An entry naming another host or port is connected to in place of the origin; when its version differs
/// from the one it was found under, the transfer switches to it: <c>h3</c> to HTTP/3 alone, with no TCP
/// fallback (<see cref="HttpVersionPreference.Http3Only" />), and <c>h1</c> or <c>h2</c> to TCP, where ALPN
/// decides between them (<see cref="HttpVersionPreference.Http11" />). An entry naming the origin itself is
/// no route; one naming <c>h3</c> makes a transfer without a version option race HTTP/3 against TCP as
/// <c>--http3</c> does, falling back to TCP when QUIC fails (measured). None is used for plain <c>http</c> or
/// for an origin a <c>--connect-to</c> mapping matches.
/// </para>
/// <para>
/// Every header is learned as having come over <c>h1</c>, from the origin, never the alternative. With
/// <c>--alt-svc ""</c> nothing is read or written. A file that cannot be opened reads as empty and one that
/// cannot be written is left as it is, both silently, as curl does.
/// </para>
/// </remarks>
internal sealed class AltSvcTransferCache : IAltSvcStore
{
    private static readonly AltSvcAlpn[] NoAlpns = [];

    private static readonly IReadOnlySet<AltSvcAlpn> NoAllowedAlpns = new HashSet<AltSvcAlpn>();

    private readonly AltSvcCache cache;

    private readonly string file;

    private readonly RequestedHttpVersion? requestedVersion;

    private readonly IReadOnlyList<string> connectToEntries;

    private AltSvcTransferCache(AltSvcCache cache, string file, RequestedHttpVersion? requestedVersion, IReadOnlyList<string> connectToEntries)
    {
        this.cache = cache;
        this.file = file;
        this.requestedVersion = requestedVersion;
        this.connectToEntries = connectToEntries;
    }

    /// <summary>
    /// Creates the cache of one transfer, reading <paramref name="file" /> into it unless it is empty.
    /// </summary>
    /// <param name="file">The <c>--alt-svc</c> value; empty for no file.</param>
    /// <param name="requestedVersion">The transfer's HTTP version option, which decides the entries it may use.</param>
    /// <param name="connectToEntries">The transfer's <c>--connect-to</c> values, which win over an alternative.</param>
    /// <param name="fileSystem">Opens the file.</param>
    /// <param name="clock">The clock expiries are counted on.</param>
    /// <returns>The cache.</returns>
    internal static async Task<AltSvcTransferCache> OpenAsync(
        string file,
        RequestedHttpVersion? requestedVersion,
        IReadOnlyList<string> connectToEntries,
        IFileSystem fileSystem,
        TimeProvider clock)
    {
        AltSvcCache cache = new(clock);
        if (file.Length > 0)
        {
            cache.ReadFile(await ReadTextAsync(file, fileSystem).ConfigureAwait(false));
        }

        return new AltSvcTransferCache(cache, file, requestedVersion, connectToEntries);
    }

    /// <summary>
    /// Gives the HTTP options a connection to <paramref name="url" /> uses, as curl 8.21.0 looks its
    /// alternative up: <paramref name="http" /> with the <see cref="HttpRequestOptions.AltSvcRoute" /> and the
    /// <see cref="HttpRequestOptions.Version" /> the alternative leaves it. The version is worked out afresh
    /// from the version option, so a redirect hop's options, which carry the first hop's, may be passed.
    /// </summary>
    /// <param name="url">The URL the connection is for.</param>
    /// <param name="http">The transfer's HTTP options.</param>
    /// <returns>The options with the route and version set.</returns>
    internal HttpRequestOptions ApplyTo(CurlUrl url, HttpRequestOptions http)
    {
        HttpVersionPreference version = HttpVersionMapping.ToHttpVersionPreference(requestedVersion);
        return MatchFor(url) switch
        {
            null => http with { AltSvcRoute = null, Version = version },
            { IsSameDestination: true } same => http with { AltSvcRoute = null, Version = SameDestinationVersion(same.Entry.DestinationAlpn, version) },
            { } other => http with { AltSvcRoute = RouteTo(other), Version = SwitchedVersion(other, version) },
        };
    }

    /// <inheritdoc />
    public IReadOnlyList<AltSvcAlternative> StoreFromResponse(CurlUrl origin, string altSvcHeader, DateTimeOffset now)
    {
        HashSet<AltSvcEntry> held = new(cache.Entries, ReferenceEqualityComparer.Instance);
        cache.ApplyHeader(altSvcHeader, AltSvcAlpn.H1, origin.IdnHost, origin.Port);
        return
        [
            .. cache.Entries
                .Where(entry => !held.Contains(entry))
                .Select(entry => new AltSvcAlternative(AltSvcAlpnToken.Format(entry.DestinationAlpn), entry.DestinationHost, entry.DestinationPort)),
        ];
    }

    /// <summary>
    /// Writes the file, replacing it, in curl's format with the platform's line endings; nothing for
    /// <c>--alt-svc ""</c>.
    /// </summary>
    /// <param name="fileSystem">Opens the file.</param>
    /// <returns>A task that completes when the file is written.</returns>
    internal async Task WriteAsync(IFileSystem fileSystem)
    {
        if (file.Length == 0)
        {
            return;
        }

        FileOpenResult opened = await fileSystem
            .OpenForWriteAsync(file, FileWriteMode.Truncate, DeferredOutputFileStream.CreateMode, CancellationToken.None)
            .ConfigureAwait(false);
        if (opened.Content is not { } content)
        {
            return;
        }

        await using (content.ConfigureAwait(false))
        {
            await content.WriteAsync(Encoding.Latin1.GetBytes(cache.FormatFile(Environment.NewLine))).ConfigureAwait(false);
        }
    }

    private static async Task<string> ReadTextAsync(string file, IFileSystem fileSystem)
    {
        FileOpenResult opened = await fileSystem.OpenForReadAsync(file, CancellationToken.None).ConfigureAwait(false);
        if (opened.Content is not { } content)
        {
            return string.Empty;
        }

        await using (content.ConfigureAwait(false))
        {
            using StreamReader reader = new(content, Encoding.Latin1, detectEncodingFromByteOrderMarks: false);
            return await reader.ReadToEndAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Tells whether a <c>--connect-to</c> mapping matches the origin, or one fails to parse, either of
    /// which decides where the connection goes in place of an alternative.
    /// </summary>
    private static bool ConnectToDecides(CurlUrl url, IReadOnlyList<string> connectToEntries) =>
        new ConnectToMappings(connectToEntries).Map(url.IdnHost, url.Port) is { IsMapped: true } or { ParseError: not null };

    private AltSvcMatch? MatchFor(CurlUrl url)
    {
        if (url.Scheme != "https" || ConnectToDecides(url, connectToEntries))
        {
            return null;
        }

        (IReadOnlyList<AltSvcAlpn> lookedUpUnder, IReadOnlySet<AltSvcAlpn> allowed) = LookupOf(requestedVersion);
        return cache.FindForOrigin(lookedUpUnder, url.IdnHost, url.Port, allowed);
    }

    /// <summary>
    /// The origin versions an entry is looked up under, in curl's order, and the destination versions the
    /// transfer may switch to, for a version option (curl 8.21.0's <c>Curl_http_neg_init</c> and
    /// <c>url_set_conn_peer</c>): <c>-0</c> and <c>--http2-prior-knowledge</c> look nothing up.
    /// </summary>
    private static (IReadOnlyList<AltSvcAlpn> LookedUpUnder, IReadOnlySet<AltSvcAlpn> Allowed) LookupOf(RequestedHttpVersion? version) =>
        version switch
        {
            null => ([AltSvcAlpn.H2, AltSvcAlpn.H1], new HashSet<AltSvcAlpn> { AltSvcAlpn.H1, AltSvcAlpn.H2, AltSvcAlpn.H3 }),
            RequestedHttpVersion.Http11 => ([AltSvcAlpn.H1], new HashSet<AltSvcAlpn> { AltSvcAlpn.H1 }),
            RequestedHttpVersion.Http2 => ([AltSvcAlpn.H2, AltSvcAlpn.H1], new HashSet<AltSvcAlpn> { AltSvcAlpn.H1, AltSvcAlpn.H2 }),
            RequestedHttpVersion.Http3 => ([AltSvcAlpn.H3, AltSvcAlpn.H2, AltSvcAlpn.H1], new HashSet<AltSvcAlpn> { AltSvcAlpn.H1, AltSvcAlpn.H2, AltSvcAlpn.H3 }),
            RequestedHttpVersion.Http3Only => ([AltSvcAlpn.H3], new HashSet<AltSvcAlpn> { AltSvcAlpn.H3 }),
            _ => (NoAlpns, NoAllowedAlpns),
        };

    /// <summary>
    /// The version a connection to the origin itself uses when its entry names <paramref name="destination" />:
    /// curl prefers that version, which for <c>h3</c> without a version option races HTTP/3 against TCP as
    /// <c>--http3</c> does (measured); any other preference leaves the version as it is.
    /// </summary>
    private HttpVersionPreference SameDestinationVersion(AltSvcAlpn destination, HttpVersionPreference version) =>
        destination == AltSvcAlpn.H3 && requestedVersion is null ? HttpVersionPreference.Http3 : version;

    /// <summary>
    /// The version a connection to another host or port uses: <paramref name="version" /> when the entry
    /// names the version it was found under, else HTTP/3 alone for <c>h3</c> and TCP, with ALPN choosing
    /// between HTTP/1.1 and HTTP/2, for <c>h1</c> and <c>h2</c>.
    /// </summary>
    private static HttpVersionPreference SwitchedVersion(AltSvcMatch match, HttpVersionPreference version) =>
        match.SourceAlpn == match.Entry.DestinationAlpn
            ? version
            : match.Entry.DestinationAlpn == AltSvcAlpn.H3 ? HttpVersionPreference.Http3Only : HttpVersionPreference.Http11;

    private static AltSvcRoute RouteTo(AltSvcMatch match) =>
        new(
            AltSvcAlpnToken.Format(match.SourceAlpn),
            new AltSvcAlternative(AltSvcAlpnToken.Format(match.Entry.DestinationAlpn), match.Entry.DestinationHost, match.Entry.DestinationPort));
}
