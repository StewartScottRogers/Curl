using System.Net;
using System.Net.Sockets;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

public sealed partial class TcpConnector
{
    /// <summary>
    /// Resolves <paramref name="host" /> for <paramref name="port" /> as curl 8.21.0's DNS cache
    /// does (BL-481): a <c>--resolve</c> entry for the host or for <c>*</c>, or a host and port
    /// this connector already resolved, answers without <see cref="IDnsResolver" /> and is
    /// reported on the target's events as <c>Hostname &lt;host&gt; was found in DNS
    /// cache</c>. A host that did not resolve is kept as a negative entry only when
    /// <see cref="CachesFailedLookUp" /> says so, and is otherwise looked up again. Every
    /// answer is then reported as curl's resolved lines (<see cref="ReportResolved" />), and
    /// written to the target's diagnostic log (<see cref="LogResolution" />, BL-920).
    /// </summary>
    /// <remarks>
    /// Under <c>-4</c> or <c>-6</c> only the chosen family's addresses are returned for a name
    /// (<see cref="AddressFamilyFilter" />). A looked-up answer is kept and reported with that
    /// family's only, as curl asks the system resolver for the one family; <c>localhost</c> is
    /// kept and reported whole, as curl answers it itself with both. A cached entry is reported
    /// whole, and one left with no address of the family is reported as <c>Negative DNS
    /// entry</c> instead, and so fails the resolve (measured on curl 8.21.0, BL-500).
    /// </remarks>
    private async ValueTask<DnsResolution> ResolveWithFailureReasonAsync(string host, int port, ConnectTarget target, CancellationToken cancellationToken) =>
        (await ResolveNotingCacheAsync(host, port, target, cancellationToken).ConfigureAwait(false)).Resolution;

    /// <summary>Marks a cancellation that arrived while a host name was being resolved (CGR-284).</summary>
    private sealed class CanceledWhileResolvingException(CancellationToken cancellationToken) : OperationCanceledException(cancellationToken);

    // Resolves as ResolveWithFailureReasonAsync does, and says whether the DNS cache answered.
    private async ValueTask<(DnsResolution Resolution, bool FromCache)> ResolveNotingCacheAsync(string host, int port, ConnectTarget target, CancellationToken cancellationToken)
    {
        var log = new NetworkDiagnosticLog(target.DiagnosticLog);
        var resolveStarted = log.IsEnabled(DiagnosticLogLevel.Info) ? timeProvider.GetTimestamp() : 0;
        var cacheKey = DnsCacheKey(host, port);
        if ((_dnsCache.Find(cacheKey) ?? _dnsCache.Find(DnsCacheKey(AnyHost, port))) is { } cached)
        {
            ReportCachedHttpsRecord(host, port, cacheKey, target);
            var fromCache = AnswerFromCache(host, cached, target.Events);
            LogResolution(log, host, port, fromCache, fromCache: true, resolveStarted);
            return (new DnsResolution(fromCache, DnsLookupFailure.None), true);
        }

        DnsResolution lookedUp;
        try
        {
            lookedUp = await LookUpAsync(host, port, target, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException canceled)
        {
            throw new CanceledWhileResolvingException(canceled.CancellationToken);
        }

        var (addresses, failure) = lookedUp;
        var answered = IsLocalhost(host) ? addresses : AddressFamilyFilter.Dialable(host, addresses, addressFamily, !FiltersAddressLiterals);
        CacheLookUp(new DnsCacheEntry(host, port, answered), cacheKey, failure, target.Events);
        var dialable = AddressFamilyFilter.Dialable(host, answered, addressFamily, !FiltersAddressLiterals);
        LogResolution(log, host, port, dialable, fromCache: false, resolveStarted);
        return (new DnsResolution(dialable, failure), false);
    }

    /// <summary>
    /// Caches a look-up's answer under <paramref name="cacheKey" /> and reports it as curl's resolved
    /// lines, or, when it answered no address, caches it as a negative entry if
    /// <see cref="CachesFailedLookUp" /> says so.
    /// </summary>
    private void CacheLookUp(DnsCacheEntry answer, string cacheKey, DnsLookupFailure failure, ITransferEvents events)
    {
        if (answer.Addresses.Count > 0)
        {
            _dnsCache.Store(cacheKey, answer);
            ReportResolved(answer, events);
        }
        else if (CachesFailedLookUp(failure))
        {
            _dnsCache.Store(cacheKey, answer);
        }
    }

    /// <summary>
    /// Tells whether a look-up that answered no address is kept as a negative DNS cache entry, as
    /// curl 8.22.0's c-ares build keeps one (ADR-0499, BL-2262): only <see cref="DnsServerResolver" />'s,
    /// the c-ares path, and not one that failed on its own configuration (exit 43), which asked nothing.
    /// The system's threaded resolver build asks again for every transfer (measured), so other resolvers
    /// keep nothing.
    /// </summary>
    private bool CachesFailedLookUp(DnsLookupFailure failure) =>
        KindOfResolver is DnsServerResolver && failure != DnsLookupFailure.BadConfiguration;

    /// <summary>
    /// Writes a resolve's answer to the diagnostic log: the addresses and elapsed milliseconds at
    /// <c>info</c>, or a <c>warning</c> when none can be dialled. The clock is read only when
    /// <c>info</c> is enabled, so a disabled log takes no timestamp.
    /// </summary>
    private void LogResolution(NetworkDiagnosticLog log, string host, int port, IReadOnlyList<IPAddress> dialable, bool fromCache, long resolveStarted)
    {
        if (dialable.Count == 0)
        {
            log.NotResolved(host, port);
        }
        else if (log.IsEnabled(DiagnosticLogLevel.Info))
        {
            log.Resolved(host, port, dialable, fromCache, timeProvider.GetElapsedTime(resolveStarted));
        }
    }

    /// <summary>
    /// Asks the resolver, with its failure reason when it gives one, with <see cref="ResolverEvents" />
    /// pointed at the target's events for the look-up. A direct connect's <c>https://</c> origin is
    /// asked of an <see cref="IHttpsOriginDnsResolver" /> with its port, so the DoH resolver sends the
    /// HTTPS query first, as curl 8.22.0's HTTPSRR build does (ADR-0727, BL-2920), and reports its
    /// <c>HTTPS-RR</c> line before the addresses' <c>Host ... was resolved.</c> (BL-2980).
    /// </summary>
    private async ValueTask<DnsResolution> LookUpAsync(string host, int port, ConnectTarget target, CancellationToken cancellationToken)
    {
        ResolverEvents?.Current = target.Events;
        if (HttpsOriginResolverFor(target) is { } httpsOriginResolver)
        {
            var addresses = await httpsOriginResolver.ResolveHttpsOriginAsync(host, port, cancellationToken).ConfigureAwait(false);
            _httpsRecordsResolved[DnsCacheKey(host, port)] = true;
            ReportHttpsRecord(host, port, httpsOriginResolver.HttpsRecordFor(host, port), target.Events);
            return new DnsResolution(dnsResolver is ShufflingDnsResolver shuffling ? shuffling.Shuffle(addresses) : addresses, addresses.Count == 0 ? DohEncodingFailureOf(host) : DnsLookupFailure.None);
        }

        return dnsResolver is IDnsResolverWithFailureReason withFailureReason
            ? await withFailureReason.ResolveWithFailureReasonAsync(host, cancellationToken).ConfigureAwait(false)
            : new DnsResolution(await dnsResolver.ResolveAsync(host, cancellationToken).ConfigureAwait(false), DnsLookupFailure.None);
    }

    // Why the DoH resolver found no address for host without sending anything: a name its A query cannot
    // encode is curl 8.22.0's exit 27 (CGR-158, BL-3915); None for any other empty answer.
    private DnsLookupFailure DohEncodingFailureOf(string host) =>
        KindOfResolver is DohDnsResolver && DnsQueryEncoder.Encode(host, DnsRecordType.A).Failure is var failure && failure != DnsMessageFailure.None
            ? failure == DnsMessageFailure.NameTooLong ? DnsLookupFailure.DohNameTooLong : DnsLookupFailure.DohBadLabel
            : DnsLookupFailure.None;

    // The resolver the look-ups reach, seen through a DNS shuffle wrapper: the kind decides the DoH, DNS-server and HTTPS-RR behaviour.
    private IDnsResolver KindOfResolver => dnsResolver is ShufflingDnsResolver shuffling ? shuffling.Inner : dnsResolver;

    // The resolver that asks a direct connect's https:// origin for its HTTPS record, or null.
    private IHttpsOriginDnsResolver? HttpsOriginResolverFor(ConnectTarget target) =>
        AsksHttpsRecords && KindOfResolver is IHttpsOriginDnsResolver httpsOriginResolver && target.Proxy is null && IsHttpsOrigin(target)
            ? httpsOriginResolver
            : null;

    // curl 8.22.0's HTTPS resolve filter sits above the address one (cf-dns.c), so its report comes
    // first; like the address report it names no IP address literal (ADR-0727's BL-2980 amendment).
    private static void ReportHttpsRecord(string host, int port, ServiceBindingRecord? record, ITransferEvents events)
    {
        if (!IPAddress.TryParse(host.Trim('[', ']'), out _))
        {
            events.ReportInfo(HttpsRecordReportLine.Format(host, port, record));
        }
    }

    // A connect whose addresses the DNS cache answers finds its HTTPS-RR answer cached too, when an
    // earlier resolve asked it: curl 8.22.0's HTTPS resolve filter, above the address one, writes its
    // own `found in DNS cache` line and then its report (ADR-0727's BL-2986 amendment).
    private void ReportCachedHttpsRecord(string host, int port, string cacheKey, ConnectTarget target)
    {
        if (HttpsOriginResolverFor(target) is { } httpsOriginResolver && _httpsRecordsResolved.ContainsKey(cacheKey))
        {
            target.Events.ReportInfo($"Hostname {host} was found in DNS cache");
            ReportHttpsRecord(host, port, httpsOriginResolver.HttpsRecordFor(host, port), target.Events);
        }
    }

    private IReadOnlyList<IPAddress> AnswerFromCache(string host, DnsCacheEntry cached, ITransferEvents events)
    {
        var dialable = AddressFamilyFilter.Dialable(host, cached.Addresses, addressFamily, !FiltersAddressLiterals);
        if (dialable.Count == 0)
        {
            events.ReportInfo("Negative DNS entry");
            return dialable;
        }

        events.ReportInfo($"Hostname {host} was found in DNS cache");
        ReportResolved(cached, events);
        return dialable;
    }

    // curl 8.21.0 answers localhost and every name under .localhost itself, with ::1 and 127.0.0.1,
    // each also with one trailing dot: with --doh-url, http://localhost.:21802/ sends no DoH query
    // and dials ::1 (CGR-159).
    internal static bool IsLocalhost(string host)
    {
        var name = host.EndsWith('.') ? host[..^1] : host;
        return string.Equals(name, "localhost", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Reports curl 8.21.0's <c>Host &lt;name&gt;:&lt;port&gt; was resolved.</c>,
    /// <c>IPv6: &lt;addresses&gt;</c> and <c>IPv4: &lt;addresses&gt;</c> lines for a cache
    /// entry, naming the entry's host as it was cached (a <c>--resolve</c> entry's as written,
    /// <c>*</c> for a wildcard) and each family's addresses in order, joined by <c>, </c>, or
    /// <c>(none)</c>. An entry named by an IP address reports none of them (measured, BL-482).
    /// </summary>
    private static void ReportResolved(DnsCacheEntry entry, ITransferEvents events)
    {
        if (IPAddress.TryParse(entry.Name, out _))
        {
            return;
        }

        events.ReportInfo($"Host {entry.Name}:{entry.Port} was resolved.");
        events.ReportInfo($"IPv6: {JoinAddresses(entry.Addresses, AddressFamily.InterNetworkV6)}");
        events.ReportInfo($"IPv4: {JoinAddresses(entry.Addresses, AddressFamily.InterNetwork)}");
    }

    private static string JoinAddresses(IReadOnlyList<IPAddress> addresses, AddressFamily family) =>
        addresses.Where(address => address.AddressFamily == family).Select(address => address.ToString()).ToArray() is { Length: > 0 } inFamily
            ? string.Join(", ", inFamily)
            : "(none)";

    private static string DnsCacheKey(string host, int port) => $"{host}:{port}";
}
