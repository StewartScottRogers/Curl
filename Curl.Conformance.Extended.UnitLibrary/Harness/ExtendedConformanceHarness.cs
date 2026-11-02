using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance.Extended.Harness;

/// <summary>
/// Runs a real curl command line in process against in-memory reference servers over TCP, TLS and
/// UDP routes - no socket, no process - and reports its exit code, standard output and standard
/// error and, per connection or datagram channel, the bytes each side sent (BL-4501).
/// </summary>
public static class ExtendedConformanceHarness
{
    /// <summary>Runs <paramref name="arguments" /> through <paramref name="curl" /> against <paramref name="routes" />.</summary>
    /// <param name="curl">The curl entry point, such as <c>Curl.Console</c>'s <c>InProcessCurl.RunAsync</c>.</param>
    /// <param name="routes">The reference servers, one per port and transport; every host name resolves to 127.0.0.1.</param>
    /// <param name="arguments">The command line, without the program name.</param>
    /// <param name="environment">The whole environment curl sees; none when <see langword="null" />.</param>
    /// <returns>What the run ended with.</returns>
    public static async Task<CurlRunResult> RunAsync(
        InProcessCurlRunner curl,
        IReadOnlyList<ReferenceRoute> routes,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string>? environment = null)
    {
        ArgumentNullException.ThrowIfNull(curl);
        ArgumentNullException.ThrowIfNull(routes);
        ArgumentNullException.ThrowIfNull(arguments);
        IReadOnlyDictionary<string, string> variables = environment ?? new Dictionary<string, string>();
        using ReferenceRun run = new();
        using MemoryStream standardOutput = new();
        using MemoryStream standardError = new();
        int exitCode = await curl(
            arguments,
            standardOutput,
            standardError,
            Stream.Null,
            new ReferenceTcpDialer(routes, run),
            new LoopbackDnsResolver(),
            new ReferenceDatagramConnector(routes, run),
            name => variables.GetValueOrDefault(name)).ConfigureAwait(false);
        IReadOnlyList<WireExchange> exchanges = await run.EndAsync().ConfigureAwait(false);
        return new CurlRunResult(exitCode, standardOutput.ToArray(), standardError.ToArray(), exchanges);
    }

    // Every host name is the reference servers' host.
    private sealed class LoopbackDnsResolver : IDnsResolver
    {
        public ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<IPAddress>>([IPAddress.Loopback]);
    }
}
