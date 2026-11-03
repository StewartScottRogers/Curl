using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.DnsQuery.Reference;

/// <summary>Runs one <c>dns://</c> command line against a <see cref="DnsReferenceServer" /> on one port, over UDP or TCP.</summary>
public static class DnsReferenceRun
{
    /// <summary>Runs <c>curl -sS</c> with <paramref name="arguments" /> against <paramref name="server" />.</summary>
    /// <param name="curl">The curl entry point.</param>
    /// <param name="server">The server.</param>
    /// <param name="transport"><see cref="ReferenceTransport.Udp" /> or <see cref="ReferenceTransport.Tcp" />: how the server is reached.</param>
    /// <param name="port">The port it answers.</param>
    /// <param name="arguments">The rest of the command line, the URL among it.</param>
    /// <returns>What the run ended with.</returns>
    public static Task<CurlRunResult> RunAsync(InProcessCurlRunner curl, DnsReferenceServer server, ReferenceTransport transport, int port, params string[] arguments)
    {
        ReferenceRoute route = transport == ReferenceTransport.Udp ? ReferenceRoute.Udp(port, server) : ReferenceRoute.Tcp(port, server);
        return ExtendedConformanceHarness.RunAsync(curl, [route], ["-sS", .. arguments]);
    }
}
