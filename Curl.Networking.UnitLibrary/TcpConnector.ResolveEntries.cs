using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

public sealed partial class TcpConnector
{
    /// <summary>
    /// Loads the <c>--resolve</c> entries into the DNS cache, as curl 8.21.0 loads them at the
    /// start of every transfer but not for a redirect it follows (measured, BL-482). Each
    /// addition is reported on <paramref name="events" /> as <c>Added
    /// &lt;host&gt;:&lt;port&gt;:&lt;addresses&gt; to DNS cache</c> (with <c> (non-permanent)</c>
    /// for a <c>+</c> entry, and <c>RESOLVE *:&lt;port&gt; using wildcard</c> after a <c>*</c>
    /// one), after <c>RESOLVE &lt;host&gt;:&lt;port&gt; - old addresses discarded</c> when the
    /// key was cached; a removal drops the key without a line. When no transfer has called
    /// this, <see cref="ConnectAsync" /> calls it on its target's events first.
    /// </summary>
    /// <param name="events">The transfer's events.</param>
    public void LoadResolveEntries(ITransferEvents events)
    {
        ArgumentNullException.ThrowIfNull(events);

        Interlocked.Exchange(ref _resolveEntriesLoaded, 1);
        foreach (var entry in _resolveOverrides.Entries)
        {
            LoadResolveEntry(entry, events);
        }
    }

    private void LoadResolveEntriesUnlessLoaded(ITransferEvents events)
    {
        if (Interlocked.CompareExchange(ref _resolveEntriesLoaded, 1, 0) == 0)
        {
            LoadResolveEntries(events);
        }
    }

    private void LoadResolveEntry(ResolveEntry entry, ITransferEvents events)
    {
        var cacheKey = DnsCacheKey(entry.Host, entry.Port);
        if (entry.IsRemoval)
        {
            _dnsCache.Remove(cacheKey);
            return;
        }

        if (_dnsCache.Contains(cacheKey))
        {
            events.ReportInfo($"RESOLVE {entry.Host}:{entry.Port} - old addresses discarded");
        }

        IReadOnlyList<IPAddress> addresses = ShufflesResolveEntryAddresses
            ? DnsAddressShuffle.Shuffle(entry.Addresses, ResolveEntryShuffleRandomBelow)
            : entry.Addresses;
        _dnsCache.Store(cacheKey, new DnsCacheEntry(entry.Host, entry.Port, addresses));
        events.ReportInfo($"[DNS] added {entry.Host}:{entry.Port}:{entry.AddressText} to cache{(entry.IsPermanent ? string.Empty : " (non-permanent)")}");
        if (entry.Host == AnyHost)
        {
            events.ReportInfo($"RESOLVE {AnyHost}:{entry.Port} using wildcard");
        }
    }
}
