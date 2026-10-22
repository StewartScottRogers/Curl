using Curl.Conformance.UnitProgram.Hashes;
using Curl.Core;

namespace Curl.Conformance.UnitProgram.Dns.Easy;

/// <summary>
/// The port of curl 8.22.0's <c>tests/unit/unit2413.c</c>: <c>Curl_peer_create</c>, here
/// <c>ConnectionPeer.Create</c>, for scheme https and port 1234 over seven host strings, <c>peer1</c> to
/// <c>peer7</c>: <c>test.curl.se</c>, <c>127.0.0.1</c>, <c>::1</c>, <c>[::1]</c>, <c>test.curl.se.</c>,
/// <c>[::1%tada]</c> and <c>::1%tada</c>. Each row checks, in the C helper's order, the scheme, the host string
/// as given, the host name, the port, the IPv6 flag and the zone id, writes the first mismatch to standard error
/// as the C helper words it, and fails <c>check failed</c> once.
/// </summary>
/// <param name="create">Makes the peer of a host string for scheme https and port 1234; <c>ConnectionPeer.Create</c> by default.</param>
public sealed class Unit2413ConnectionPeer(Func<string, Unit2413ConnectionPeer.Peer> create) : IUnitProgram
{
    /// <summary>Creates the port over <c>ConnectionPeer.Create</c>.</summary>
    public Unit2413ConnectionPeer()
        : this(CreateThroughCore)
    {
    }

    /// <summary>The fields of a <c>struct Curl_peer</c> the C helper checks.</summary>
    /// <param name="Scheme">The scheme.</param>
    /// <param name="UserHostname">The host string as given.</param>
    /// <param name="Hostname">The host name to resolve.</param>
    /// <param name="Port">The port.</param>
    /// <param name="IsIpv6">Whether the host is an IPv6 address.</param>
    /// <param name="ZoneId">The zone id, or <see langword="null" />.</param>
    public readonly record struct Peer(string Scheme, string UserHostname, string Hostname, int Port, bool IsIpv6, string? ZoneId);

    private static readonly (string Name, string Host, string Hostname, bool IsIpv6, string? ZoneId)[] Rows =
    [
        ("peer1", "test.curl.se", "test.curl.se", false, null),
        ("peer2", "127.0.0.1", "127.0.0.1", false, null),
        ("peer3", "::1", "::1", true, null),
        ("peer4", "[::1]", "::1", true, null),
        ("peer5", "test.curl.se.", "test.curl.se.", false, null),
        ("peer6", "[::1%tada]", "::1", true, "tada"),
        ("peer7", "::1%tada", "::1", true, "tada"),
    ];

    /// <inheritdoc />
    public string ProgramName => "unit2413";

    /// <inheritdoc />
    public async Task<UnitProgramResult> RunAsync(UnitProgramContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        UnitChecks checks = new(context.StandardError, ProgramName);
        foreach ((string name, string host, string hostname, bool isIpv6, string? zoneId) in Rows)
        {
            string? mismatch = Mismatch(name, create(host), new Peer("https", host, hostname, 1234, isIpv6, zoneId));
            if (mismatch is not null)
            {
                await context.StandardError.WriteAsync(mismatch).ConfigureAwait(false);
            }

            checks.FailUnless(mismatch is null, "check failed");
        }

        return checks.Result;
    }

    /// <summary>The C helper's first failing check of <paramref name="peer" /> against <paramref name="expected" />, worded as it is, or <see langword="null" />.</summary>
    /// <param name="name">The row's name, <c>peer1</c> to <c>peer7</c>.</param>
    /// <param name="peer">The peer made.</param>
    /// <param name="expected">The peer the row expects.</param>
    /// <returns>The mismatch text, or <see langword="null" /> when every check holds.</returns>
    private static string? Mismatch(string name, Peer peer, Peer expected) =>
        peer.Scheme != expected.Scheme ? $"{name}: has wrong scheme"
        : !string.Equals(peer.UserHostname, expected.UserHostname, StringComparison.OrdinalIgnoreCase) ? $"{name}: user_hostname={peer.UserHostname}, expected {expected.Hostname}"
        : !string.Equals(peer.Hostname, expected.Hostname, StringComparison.OrdinalIgnoreCase) ? $"{name}: hostname={peer.Hostname}, expected {expected.Hostname}"
        : peer.Port != expected.Port ? $"{name}: port={peer.Port}, expected {expected.Port}"
        : peer.IsIpv6 != expected.IsIpv6 ? $"{name}: ipv6={(peer.IsIpv6 ? 1 : 0)}, expected {(expected.IsIpv6 ? 1 : 0)}"
        : !string.Equals(peer.ZoneId, expected.ZoneId, StringComparison.OrdinalIgnoreCase) ? $"{name}: zoneid={peer.ZoneId}, expected {expected.ZoneId ?? "nothing"}"
        : null;

    private static Peer CreateThroughCore(string host)
    {
        ConnectionPeer peer = ConnectionPeer.Create("https", host, 1234);
        return new Peer(peer.Scheme, peer.UserHostname, peer.Hostname, peer.Port, peer.IsIpv6, peer.ZoneId);
    }
}
