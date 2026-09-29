using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// The hand-built DNS client behind <c>--dns-servers</c>, <c>--dns-interface</c>,
/// <c>--dns-ipv4-addr</c> and <c>--dns-ipv6-addr</c> (ADR-0170, BL-694): it asks the given
/// servers, or the system's when only a binding option is given, for AAAA and A records at once
/// over UDP, as curl 8.22.0's c-ares 1.34.8 build does on every platform.
/// </summary>
/// <remarks>
/// <para>
/// Each query (<see cref="DnsServerQuery" />) goes to the servers in list order, round after round,
/// <see cref="Rounds" /> rounds in all; each attempt waits <see cref="FirstTimeout" /> in the first
/// round, doubling each round, on the injected <see cref="TimeProvider" />. A truncated UDP reply is
/// asked again over TCP at once. An answer, NOERROR with no data, or NXDOMAIN ends the query; a
/// timeout, an unreachable server, an unreadable reply or another response code moves it to the next
/// server. The query socket is bound per <see cref="DnsSourceBinding" />.
/// </para>
/// <para>
/// A name that does not resolve reports the last query's <see cref="DnsLookupFailure" />; a list or an
/// address that does not parse reports <see cref="DnsLookupFailure.BadConfiguration" /> before anything
/// is sent. An IP address literal is returned as it is, and <c>localhost</c> and names under
/// <c>.localhost</c> as <c>::1</c> and <c>127.0.0.1</c>, without a query, as curl answers them itself.
/// </para>
/// </remarks>
public sealed class DnsServerResolver : IDnsResolverWithFailureReason
{
    /// <summary>How many times each server is tried, one round of the list each.</summary>
    public const int Rounds = 3;

    /// <summary>How long the first round's attempt waits for a reply; each later round doubles it.</summary>
    public static readonly TimeSpan FirstTimeout = TimeSpan.FromSeconds(2);

    private readonly DnsServerResolverOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly IDnsSocketOpener _socketOpener;
    private readonly Func<IReadOnlyList<IPEndPoint>> _listSystemServers;
    private readonly Func<string, IReadOnlyList<IPAddress>?> _findInterfaceAddresses;
    private readonly Action<Span<byte>> _fillRandom;
    private readonly ConcurrentDictionary<IPEndPoint, byte[]> _clientCookies = new();

    /// <summary>Initializes a resolver that opens real sockets and reads the system's DNS servers and interfaces.</summary>
    /// <param name="options">The c-ares options and the <c>-4</c> or <c>-6</c> choice.</param>
    /// <param name="timeProvider">Times each attempt.</param>
    [ExcludeFromCodeCoverage(Justification = "Only passes the production pieces to the tested constructor.")]
    public DnsServerResolver(DnsServerResolverOptions options, TimeProvider timeProvider)
        : this(options, timeProvider, new DnsSocketOpener(), SystemDnsServers.List, FindSystemInterfaceAddresses, RandomNumberGenerator.Fill)
    {
    }

    /// <summary>Initializes a resolver over the given pieces, so a test drives it without a network.</summary>
    /// <param name="options">The c-ares options and the <c>-4</c> or <c>-6</c> choice.</param>
    /// <param name="timeProvider">Times each attempt.</param>
    /// <param name="socketOpener">Opens each query's socket.</param>
    /// <param name="listSystemServers">Lists the servers used when no <c>--dns-servers</c> is given.</param>
    /// <param name="findInterfaceAddresses">Finds an interface's addresses by name, or <see langword="null" />.</param>
    /// <param name="fillRandom">Fills query IDs and client cookies with random bytes.</param>
    internal DnsServerResolver(
        DnsServerResolverOptions options,
        TimeProvider timeProvider,
        IDnsSocketOpener socketOpener,
        Func<IReadOnlyList<IPEndPoint>> listSystemServers,
        Func<string, IReadOnlyList<IPAddress>?> findInterfaceAddresses,
        Action<Span<byte>> fillRandom)
    {
        _options = options;
        _timeProvider = timeProvider;
        _socketOpener = socketOpener;
        _listSystemServers = listSystemServers;
        _findInterfaceAddresses = findInterfaceAddresses;
        _fillRandom = fillRandom;
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken) =>
        (await ResolveWithFailureReasonAsync(host, cancellationToken).ConfigureAwait(false)).Addresses;

    /// <inheritdoc />
    public async ValueTask<DnsResolution> ResolveWithFailureReasonAsync(string host, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);

        if (AnsweredWithoutQuery(host) is { } answered)
        {
            return new DnsResolution(answered, DnsLookupFailure.None);
        }

        if (!TryConfigure(out var servers, out var binding))
        {
            return new DnsResolution([], DnsLookupFailure.BadConfiguration);
        }

        var lookups = AddressTypes().Select(type => QueryAsync(servers, binding, host, type, cancellationToken).AsTask()).ToArray();
        var outcomes = await Task.WhenAll(lookups).ConfigureAwait(false);
        var addresses = outcomes.SelectMany(outcome => outcome.Answer?.Addresses ?? []).ToArray();
        return addresses.Length > 0
            ? new DnsResolution(addresses, DnsLookupFailure.None)
            : new DnsResolution([], outcomes[^1].Failure);
    }

    /// <summary>
    /// Looks up the SRV records of <paramref name="serviceName" /> (e.g. <c>_kerberos._udp.EXAMPLE.COM</c>)
    /// through the same servers, rounds and binding as a host name, for Kerberos KDC location (BL-689).
    /// </summary>
    /// <param name="serviceName">The service's owner name.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The SRV records, or why there are none.</returns>
    public async ValueTask<DnsServiceLookup> ResolveServiceAsync(string serviceName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        if (!TryConfigure(out var servers, out var binding))
        {
            return new DnsServiceLookup([], DnsLookupFailure.BadConfiguration);
        }

        var outcome = await QueryAsync(servers, binding, serviceName, DnsRecordType.Srv, cancellationToken).ConfigureAwait(false);
        return new DnsServiceLookup(outcome.Answer?.ServiceRecords ?? [], outcome.Failure);
    }

    [ExcludeFromCodeCoverage(Justification = "Reads this machine's interfaces; the lookup it feeds is tested through the injected one.")]
    private static IReadOnlyList<IPAddress>? FindSystemInterfaceAddresses(string interfaceName) =>
        SystemNetworkInterfaceLookup.ListSystemInterfaces()
            .Where(networkInterface => string.Equals(networkInterface.Name, interfaceName, StringComparison.OrdinalIgnoreCase))
            .Select(networkInterface => networkInterface.Addresses)
            .FirstOrDefault();

    private static IPAddress[]? AnsweredWithoutQuery(string host)
    {
        if (IPAddress.TryParse(host, out var literal))
        {
            return [literal];
        }

        var isLocalhost = string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase);
        return isLocalhost ? [IPAddress.IPv6Loopback, IPAddress.Loopback] : null;
    }

    private DnsRecordType[] AddressTypes() => _options.AddressFamily switch
    {
        AddressFamily.InterNetwork => [DnsRecordType.A],
        AddressFamily.InterNetworkV6 => [DnsRecordType.Aaaa],
        _ => [DnsRecordType.Aaaa, DnsRecordType.A],
    };

    private bool TryConfigure([NotNullWhen(true)] out IReadOnlyList<IPEndPoint>? servers, [NotNullWhen(true)] out DnsSourceBinding? binding)
    {
        servers = null;
        if (!DnsSourceBinding.TryParse(_options.InterfaceName, _options.IPv4Address, _options.IPv6Address, out binding))
        {
            return false;
        }

        if (_options.ServerList is null)
        {
            servers = _listSystemServers();
            return true;
        }

        return DnsServerList.TryParse(_options.ServerList, out servers);
    }

    /// <summary>Asks the servers, round after round, until one gives a final outcome.</summary>
    private async ValueTask<DnsQueryOutcome> QueryAsync(
        IReadOnlyList<IPEndPoint> servers,
        DnsSourceBinding binding,
        string name,
        DnsRecordType recordType,
        CancellationToken cancellationToken)
    {
        if (DnsQueryEncoder.Encode(name, recordType).Failure != DnsMessageFailure.None)
        {
            return new DnsQueryOutcome(null, DnsLookupFailure.BadName);
        }

        var id = NextId();
        var outcome = new DnsQueryOutcome(null, DnsLookupFailure.Unreachable);
        for (var attempt = 0; attempt < Rounds * servers.Count && !outcome.IsFinal; attempt++)
        {
            var timeout = FirstTimeout * (1 << (attempt / servers.Count));
            var question = new DnsQuestion(name, recordType, id);
            outcome = await AskAsync(servers[attempt % servers.Count], binding, question, timeout, cancellationToken).ConfigureAwait(false);
        }

        return outcome;
    }

    /// <summary>Asks one server once, over UDP and then TCP when the reply is truncated, within <paramref name="timeout" />.</summary>
    private async ValueTask<DnsQueryOutcome> AskAsync(
        IPEndPoint server,
        DnsSourceBinding binding,
        DnsQuestion question,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var udpQuery = DnsServerQuery.Build(question.Name, question.RecordType, question.Id, ClientCookieFor(server)).Bytes;
        var localAddress = binding.LocalAddressFor(server.AddressFamily, _findInterfaceAddresses);
        using var timeoutSource = new CancellationTokenSource(timeout, _timeProvider);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(timeoutSource.Token, cancellationToken);
        try
        {
            var reply = await ExchangeOverUdpAsync(server, localAddress, udpQuery, linkedSource.Token).ConfigureAwait(false);
            return DnsServerQuery.Match(udpQuery, reply) == DnsReplyMatch.Complete
                ? DnsServerQuery.Read(reply, question.RecordType)
                : await AskOverTcpAsync(server, localAddress, question, linkedSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new DnsQueryOutcome(null, DnsLookupFailure.Timeout);
        }
        catch (Exception exception) when (exception is SocketException or IOException)
        {
            return new DnsQueryOutcome(null, DnsLookupFailure.Unreachable);
        }
    }

    /// <summary>Sends the query and returns the first reply from the server that answers it, ignoring any other datagram.</summary>
    private async ValueTask<byte[]> ExchangeOverUdpAsync(IPEndPoint server, IPAddress localAddress, byte[] query, CancellationToken cancellationToken)
    {
        var channel = _socketOpener.OpenDatagramChannel(server, localAddress);
        await using (channel.ConfigureAwait(false))
        {
            await channel.SendAsync(query, server, cancellationToken).ConfigureAwait(false);
            var buffer = new byte[65535];
            while (true)
            {
                var received = await channel.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
                var reply = buffer.AsSpan(0, received.Length);
                if (IsFrom(server, received.RemoteEndPoint) && DnsServerQuery.Match(query, reply) != DnsReplyMatch.Mismatch)
                {
                    return reply.ToArray();
                }
            }
        }
    }

    /// <summary>Asks the query again over TCP, each message after its two-byte length (RFC 1035 section 4.2.2).</summary>
    private async ValueTask<DnsQueryOutcome> AskOverTcpAsync(IPEndPoint server, IPAddress localAddress, DnsQuestion question, CancellationToken cancellationToken)
    {
        var query = DnsServerQuery.Build(question.Name, question.RecordType, question.Id, []).Bytes;
        var stream = await _socketOpener.ConnectStreamAsync(server, localAddress, cancellationToken).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            var framed = new byte[2 + query.Length];
            BinaryPrimitives.WriteUInt16BigEndian(framed, (ushort)query.Length);
            query.CopyTo(framed, 2);
            await stream.WriteAsync(framed, cancellationToken).ConfigureAwait(false);

            var length = new byte[2];
            await stream.ReadExactlyAsync(length, cancellationToken).ConfigureAwait(false);
            var reply = new byte[BinaryPrimitives.ReadUInt16BigEndian(length)];
            await stream.ReadExactlyAsync(reply, cancellationToken).ConfigureAwait(false);
            return DnsServerQuery.Match(query, reply) == DnsReplyMatch.Complete
                ? DnsServerQuery.Read(reply, question.RecordType)
                : new DnsQueryOutcome(null, DnsLookupFailure.BadReply);
        }
    }

    /// <summary>
    /// Tells whether a datagram came from <paramref name="server" />: the same port and address
    /// bytes, ignoring an IPv6 scope ID one side may carry and the other not.
    /// </summary>
    private static bool IsFrom(IPEndPoint server, EndPoint source) =>
        source is IPEndPoint sender
            && sender.Port == server.Port
            && sender.Address.GetAddressBytes().AsSpan().SequenceEqual(server.Address.GetAddressBytes());

    private ushort NextId()
    {
        Span<byte> id = stackalloc byte[2];
        _fillRandom(id);
        return BinaryPrimitives.ReadUInt16BigEndian(id);
    }

    /// <summary>The client cookie for <paramref name="server" />, made once per server and kept, as c-ares keeps one.</summary>
    private byte[] ClientCookieFor(IPEndPoint server) => _clientCookies.GetOrAdd(server, _ =>
    {
        var cookie = new byte[DnsServerQuery.ClientCookieLength];
        _fillRandom(cookie);
        return cookie;
    });

    /// <summary>One query's name, type and ID, the same on every attempt.</summary>
    private sealed record DnsQuestion(string Name, DnsRecordType RecordType, ushort Id);
}
