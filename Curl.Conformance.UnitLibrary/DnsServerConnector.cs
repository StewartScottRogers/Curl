using System.Net;
using System.Net.Sockets;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// An <see cref="IDatagramConnector"/> that emulates upstream's DNS test server, <c>dnsd</c>
/// (<c>tests/server/dnsd.c</c> at <c>curl-8_22_0</c>), on <see cref="DnsPort"/>, and passes an open
/// to any other port to the connector it wraps. No socket is opened.
/// </summary>
/// <remarks>
/// <para>
/// dnsd answers every name alike from the case's <c>&lt;reply&gt;&lt;dns&gt;</c> part, which
/// <c>runner.pm</c> writes to <c>dnsd.cmd</c>: an <c>A: &lt;IPv4 address&gt;</c> line gives an A
/// query one answer, an <c>AAAA: &lt;IPv6 address&gt;</c> line (brackets allowed) an AAAA query
/// one, and a line whose address does not parse (<c>A: none</c>), or none, leaves the answer
/// section empty under rcode 0. Its other lines (<c>HTTPS:</c>, <c>Delay-*</c>, <c>Rcode-*</c>)
/// are not emulated: no case at <c>curl-8_22_0</c> uses them.
/// </para>
/// <para>
/// <see cref="QueryLog"/> holds the <c>QNAME &lt;name&gt; QTYPE &lt;type&gt;</c> line dnsd appends
/// to <c>dnsd.input</c> for each query, across channels, for <c>&lt;verify&gt;&lt;dns&gt;</c>.
/// </para>
/// </remarks>
/// <param name="testCase">The test case, parsed after <see cref="UpstreamTestFileExpander"/> has expanded it.</param>
/// <param name="otherPorts">Where an open to any port but <see cref="DnsPort"/> goes.</param>
public sealed class DnsServerConnector(UpstreamTestCase testCase, IDatagramConnector otherPorts) : IDatagramConnector
{
    /// <summary>The port of upstream's DNS server, <c>%DNSPORT</c>; servers.pm picks a free port, and no other stand-in uses this one.</summary>
    public const int DnsPort = 9017;

    /// <summary>The <c>&lt;server&gt;</c> names this stand-in emulates.</summary>
    public static readonly IReadOnlyList<string> EmulatedServers = ["dns"];

    private readonly StringBuilder log = new();

    /// <summary>The <c>dnsd.input</c> dump: one <c>QNAME &lt;name&gt; QTYPE &lt;A|AAAA|HTTPS|&lt;unknown&gt;&gt;</c> line per query.</summary>
    public string QueryLog
    {
        get
        {
            lock (log)
            {
                return log.ToString();
            }
        }
    }

    /// <summary>The address an A query is answered with, or <see langword="null"/> for none.</summary>
    internal IPAddress? Ipv4Answer { get; } = ReadAnswer(testCase, "A: ", AddressFamily.InterNetwork);

    /// <summary>The address an AAAA query is answered with, or <see langword="null"/> for none.</summary>
    internal IPAddress? Ipv6Answer { get; } = ReadAnswer(testCase, "AAAA: ", AddressFamily.InterNetworkV6);

    /// <summary>Opens a channel to the dnsd emulation when <paramref name="port"/> is <see cref="DnsPort"/>.</summary>
    /// <param name="host">The server's address; an IP literal is the answer's source, any other name loopback.</param>
    /// <param name="port">The UDP port.</param>
    /// <param name="cancellationToken">Passed on.</param>
    /// <returns>The open channel, or what the wrapped connector returns.</returns>
    public ValueTask<DatagramOpenResult> OpenAsync(string host, int port, CancellationToken cancellationToken) =>
        port == DnsPort
            ? ValueTask.FromResult(DatagramOpenResult.Opened(new DnsServerChannel(this, IPAddress.TryParse(host, out IPAddress? address) ? address : IPAddress.Loopback)))
            : otherPorts.OpenAsync(host, port, cancellationToken);

    /// <summary>Appends one query's line to <see cref="QueryLog"/>.</summary>
    internal void LogQuery(string name, string type)
    {
        lock (log)
        {
            log.Append($"QNAME {name} QTYPE {type}\n");
        }
    }

    // dnsd keeps the last line of each kind, and inet_pton failing leaves no answer.
    private static IPAddress? ReadAnswer(UpstreamTestCase testCase, string prefix, AddressFamily family) =>
        UpstreamTestPartBodies.Lines(testCase.Find("reply", "dns"))
            .Where(line => line.StartsWith(prefix, StringComparison.Ordinal))
            .Select(line => IPAddress.TryParse(line[prefix.Length..].Trim('[', ']'), out IPAddress? address) && address.AddressFamily == family ? address : null)
            .LastOrDefault();
}
