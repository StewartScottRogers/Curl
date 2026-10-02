using System.Collections.Concurrent;

namespace Curl.Networking;

/// <summary>
/// curl 8.21.0's DNS cache for one run: the host and port pairs resolved, and the <c>--resolve</c>
/// entries loaded, keyed on <c>host:port</c> without regard to letter case. curl keeps one for the
/// whole command line, so a later <c>-:</c>/<c>--next</c> option group answers a host an earlier
/// group resolved, or an earlier group's <c>--resolve</c> entry (a <c>+</c> one included), from it
/// and reports <c>Hostname H was found in DNS cache</c> (measured, BL-1053). Hand one to every
/// group's <see cref="TcpConnector" /> to share it; a connector given none keeps one of its own.
/// </summary>
public sealed class DnsCache
{
    private readonly ConcurrentDictionary<string, DnsCacheEntry> _entries = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets the entry cached for <paramref name="key" />, or <see langword="null" />.</summary>
    internal DnsCacheEntry? Find(string key) => _entries.GetValueOrDefault(key);

    /// <summary>Tells whether an entry is cached for <paramref name="key" />.</summary>
    internal bool Contains(string key) => _entries.ContainsKey(key);

    /// <summary>Caches <paramref name="entry" /> for <paramref name="key" />, in place of any entry there.</summary>
    internal void Store(string key, DnsCacheEntry entry) => _entries[key] = entry;

    /// <summary>Drops the entry cached for <paramref name="key" />, if there is one.</summary>
    internal void Remove(string key) => _entries.TryRemove(key, out _);
}
