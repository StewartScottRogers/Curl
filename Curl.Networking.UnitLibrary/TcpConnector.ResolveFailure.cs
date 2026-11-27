using Curl.Protocol.Abstractions;

namespace Curl.Networking;

public sealed partial class TcpConnector
{
    /// <summary>
    /// Fails a connect whose host did not resolve with exit 6 (or 43), first writing, unless the DNS
    /// cache answered, the <c>-v</c> lines curl 8.21.0 writes once its resolver answered nothing for a host
    /// (measured, BL-1157 Notes): <c>Could not resolve host: &lt;host&gt;</c>, twice from the
    /// system's threaded resolver and so from every resolver but DoH and an <see cref="IDohUrlDnsResolver" />,
    /// which write it once (ADR-0366, BL-3916), then
    /// <c>Could not resolve: &lt;host&gt;:&lt;port&gt;</c>, around which a
    /// <see cref="DnsFilterTraceEvents" /> writes its own. A negative DNS cache entry writes
    /// <c>Could not resolve host: &lt;host&gt;</c> once, <c>Could not resolve: &lt;host&gt;:&lt;port&gt;</c>
    /// and <c>Could not resolve: &lt;host&gt;</c> (measured, BL-1181 Notes). A
    /// <see cref="ConnectTarget.IsForwardProxy" /> target names the proxy, so its name failing is exit 5
    /// <c>Could not resolve proxy: &lt;proxy&gt;</c>, as curl 8.22.0 reports <c>-x</c>, <c>--proxy1.0</c> and an
    /// <c>https://</c> proxy (measured, BL-3168).
    /// </summary>
    /// <summary>
    /// Reports an empty answer: exit 7 for an IP address literal that <c>-4</c> or <c>-6</c> leaves nothing to dial off
    /// Windows (curl 8.18.0 on Linux, CGR-186), otherwise <see cref="HostNotResolved" />.
    /// </summary>
    private ConnectResult NothingToDial(ConnectTarget target, ConnectDestination destination, DnsLookupFailure failure, bool fromCache) =>
        AddressFamilyFilter.IsRefusedLiteral(destination.Host, addressFamily, !FiltersAddressLiterals)
            ? ConnectResult.Failed(CurlExitCode.CouldntConnect, $"Failed to connect to {HostWithoutZoneId(target.Host)}:{target.Port} after 0 ms: Could not connect to server")
            : HostNotResolved(target, destination, failure, fromCache);

    private ConnectResult HostNotResolved(ConnectTarget target, ConnectDestination destination, DnsLookupFailure failure, bool fromCache)
    {
        var events = target.Events;
        var (notResolved, subject) = target.IsForwardProxy ? (CurlExitCode.CouldntResolveProxy, "proxy") : (CurlExitCode.CouldntResolveHost, "host");
        var (exitCode, message) = NameResolutionFailure.Describe(notResolved, subject, destination.Host, destination.Port, failure);
        if (fromCache)
        {
            ReportNegativeEntryFailed(events, destination, message);
        }
        else if (exitCode == notResolved)
        {
            ReportLookUpFailed(events, destination, message);
        }

        return ConnectResult.Failed(exitCode, message);
    }

    /// <summary>
    /// Reports the refusal to connect to a <c>.onion</c> name, made before any <c>--resolve</c> entry, cached answer or
    /// look-up, as curl 8.21.0 reports it before its exit 6 <c>Not resolving .onion address (RFC 7686)</c>: the
    /// <c>-v</c> lines <c>Not resolving .onion address (RFC 7686)</c>, <c>Could not resolve:
    /// &lt;host&gt;:&lt;port&gt;</c> and <c>Could not resolve: &lt;host&gt;</c> (measured, BL-1394).
    /// </summary>
    private static void ReportOnionRefused(ITransferEvents events, ConnectDestination destination)
    {
        events.ReportInfo(OnionAddress.RefusalMessage);
        events.ReportInfo(DnsFilterTraceEvents.CouldNotResolveLine(destination.Host, destination.Port));
        events.ReportInfo(DnsFilterTraceEvents.CouldNotResolveLine(destination.Host));
    }

    private static void ReportNegativeEntryFailed(ITransferEvents events, ConnectDestination destination, string message)
    {
        events.ReportInfo(message);
        events.ReportInfo(DnsFilterTraceEvents.CouldNotResolveLine(destination.Host, destination.Port));
        events.ReportInfo(DnsFilterTraceEvents.CouldNotResolveLine(destination.Host));
    }

    private void ReportLookUpFailed(ITransferEvents events, ConnectDestination destination, string message)
    {
        events.ReportInfo(message);
        if (KindOfResolver is not (DohDnsResolver or IDohUrlDnsResolver))
        {
            events.ReportInfo(message);
        }

        events.ReportInfo(DnsFilterTraceEvents.CouldNotResolveLine(destination.Host, destination.Port));
    }
}
