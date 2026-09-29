using System.Text;
using Curl.Core.AltSvc;
using Curl.Networking;
using Curl.Protocol.Abstractions;
using AltSvcAlternative = Curl.Protocol.Abstractions.AltSvcAlternative;

namespace Curl.Console;

/// <summary>
/// One <c>http</c> or <c>https</c> transfer's alt-svc cache (<c>--alt-svc</c>), as curl 8.21.0 gives each
/// transfer's handle its own: the file read before the transfer, the <see cref="AltSvcRoute" /> the transfer
/// connects through, the <c>Alt-Svc</c> headers it learns from, and the file written when it ends.
/// </summary>
/// <remarks>
/// Measured on 2026-09-29 (BL-623 Notes, ADR-0213). Only <c>h1</c> alternatives are used for now, as curl skips
/// an alternative whose HTTP version the transfer may not use (<c>h2</c> and <c>h3</c> are BL-733's), and none
/// is used for plain <c>http</c>, for an origin a <c>--connect-to</c> mapping matches, or for an entry naming
/// the origin itself. Every header is learned as having come over <c>h1</c>, from the origin, never the
/// alternative. With <c>--alt-svc ""</c> nothing is read or written. A file that cannot be opened reads as
/// empty and one that cannot be written is left as it is, both silently, as curl does.
/// </remarks>
internal sealed class AltSvcTransferCache : IAltSvcStore
{
    /// <summary>The ALPN ID of the one version alternatives are looked up for and used over.</summary>
    private const string H1 = "h1";

    private static readonly IReadOnlySet<AltSvcAlpn> UsableAlpns = new HashSet<AltSvcAlpn> { AltSvcAlpn.H1 };

    private readonly AltSvcCache cache;

    private readonly string file;

    private AltSvcTransferCache(AltSvcCache cache, string file)
    {
        this.cache = cache;
        this.file = file;
    }

    /// <summary>
    /// Creates the cache of one transfer, reading <paramref name="file" /> into it unless it is empty.
    /// </summary>
    /// <param name="file">The <c>--alt-svc</c> value; empty for no file.</param>
    /// <param name="fileSystem">Opens the file.</param>
    /// <param name="clock">The clock expiries are counted on.</param>
    /// <returns>The cache.</returns>
    internal static async Task<AltSvcTransferCache> OpenAsync(string file, IFileSystem fileSystem, TimeProvider clock)
    {
        AltSvcCache cache = new(clock);
        if (file.Length > 0)
        {
            cache.ReadFile(await ReadTextAsync(file, fileSystem).ConfigureAwait(false));
        }

        return new AltSvcTransferCache(cache, file);
    }

    /// <summary>
    /// Finds the alternative a transfer to <paramref name="url" /> connects to, as curl 8.21.0 looks it up.
    /// </summary>
    /// <param name="url">The transfer's URL.</param>
    /// <param name="connectToEntries">The transfer's <c>--connect-to</c> values, which win over an alternative.</param>
    /// <returns>The route, or <see langword="null" /> to connect to the origin as usual.</returns>
    internal AltSvcRoute? RouteFor(CurlUrl url, IReadOnlyList<string> connectToEntries)
    {
        if (url.Scheme != "https" || ConnectToDecides(url, connectToEntries))
        {
            return null;
        }

        AltSvcEntry? entry = cache.Find(AltSvcAlpn.H1, url.IdnHost, url.Port, UsableAlpns);
        return entry is null || IsOrigin(entry) ? null : RouteTo(entry);
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

    private static AltSvcRoute RouteTo(AltSvcEntry entry) =>
        new(H1, new AltSvcAlternative(H1, entry.DestinationHost, entry.DestinationPort));

    private static bool IsOrigin(AltSvcEntry entry) =>
        entry.DestinationPort == entry.SourcePort
        && string.Equals(entry.DestinationHost, entry.SourceHost, StringComparison.OrdinalIgnoreCase);
}
