using System.Net;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance.Extended.Harness;

/// <summary>
/// Runs a real curl command line in process against in-memory reference servers over TCP, TLS and
/// UDP routes - no socket, no process - and reports its exit code, standard output and standard
/// error and, per connection or datagram channel, the bytes each side sent (BL-4501). A case that
/// runs an extension handler which starts a program hands in a stand-in process starter (BL-4577).
/// </summary>
public static class ExtendedConformanceHarness
{
    /// <summary>Runs <paramref name="arguments" /> through <paramref name="curl" /> against <paramref name="routes" />.</summary>
    /// <param name="curl">The curl entry point, such as <c>Curl.Console</c>'s <c>InProcessCurl.RunAsync</c>.</param>
    /// <param name="routes">The reference servers, one per port and transport; every host name resolves to 127.0.0.1.</param>
    /// <param name="arguments">The command line, without the program name.</param>
    /// <param name="environment">The whole environment curl sees; none when <see langword="null" />.</param>
    /// <param name="timeProvider">The clock every timer of the run runs on, so a test drives <c>-m</c> with a fake one and no real wait; <see cref="TimeProvider.System" /> when <see langword="null" />.</param>
    /// <returns>What the run ended with.</returns>
    public static Task<CurlRunResult> RunAsync(
        InProcessCurlRunner curl,
        IReadOnlyList<ReferenceRoute> routes,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string>? environment = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(curl);
        ArgumentNullException.ThrowIfNull(routes);
        ArgumentNullException.ThrowIfNull(arguments);
        return RunOverReferenceServersAsync(
            (standardOutput, standardError, tcpDialer, dnsResolver, datagramConnector, readEnvironmentVariable, clock) =>
                curl(arguments, standardOutput, standardError, Stream.Null, tcpDialer, dnsResolver, datagramConnector, readEnvironmentVariable, clock),
            routes,
            environment,
            timeProvider);
    }

    /// <summary>
    /// Runs <paramref name="arguments" /> through <paramref name="curl" /> against <paramref name="routes" />, with every
    /// program an extension handler runs - an <c>mcp+stdio://</c> server, a <c>curl-proto-&lt;scheme&gt;</c> helper -
    /// started by <paramref name="processStarter" /> and looked for in the environment it reads, so no real process starts (ADR-1184).
    /// </summary>
    /// <param name="curl">The curl entry point, <c>Curl.Console</c>'s <c>InProcessCurl.RunAsync</c> overload that takes a process starter.</param>
    /// <param name="routes">The reference servers, one per port and transport; every host name resolves to 127.0.0.1.</param>
    /// <param name="arguments">The command line, without the program name.</param>
    /// <param name="processStarter">The stand-in that starts the programs the extension handlers run.</param>
    /// <param name="environment">The whole environment curl sees; none when <see langword="null" />.</param>
    /// <param name="timeProvider">The clock every timer of the run runs on; <see cref="TimeProvider.System" /> when <see langword="null" />.</param>
    /// <returns>What the run ended with.</returns>
    public static Task<CurlRunResult> RunAsync(
        InProcessCurlProcessRunner curl,
        IReadOnlyList<ReferenceRoute> routes,
        IReadOnlyList<string> arguments,
        IExtensionProcessStarter processStarter,
        IReadOnlyDictionary<string, string>? environment = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(curl);
        ArgumentNullException.ThrowIfNull(routes);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(processStarter);
        return RunOverReferenceServersAsync(
            (standardOutput, standardError, tcpDialer, dnsResolver, datagramConnector, readEnvironmentVariable, clock) =>
                curl(arguments, standardOutput, standardError, Stream.Null, tcpDialer, dnsResolver, datagramConnector, readEnvironmentVariable, clock, processStarter),
            routes,
            environment,
            timeProvider);
    }

    // Runs curl over the reference servers' dialer, resolver and datagram connector and gathers what it wrote and sent.
    private static async Task<CurlRunResult> RunOverReferenceServersAsync(
        Func<Stream, Stream, ITcpDialer, IDnsResolver, IDatagramConnector, Func<string, string?>, TimeProvider, Task<int>> runCurl,
        IReadOnlyList<ReferenceRoute> routes,
        IReadOnlyDictionary<string, string>? environment,
        TimeProvider? timeProvider)
    {
        IReadOnlyDictionary<string, string> variables = environment ?? new Dictionary<string, string>();
        using ReferenceRun run = new();
        using MemoryStream standardOutput = new();
        using MemoryStream standardError = new();
        int exitCode = await runCurl(
            standardOutput,
            standardError,
            new ReferenceTcpDialer(routes, run),
            new LoopbackDnsResolver(),
            new ReferenceDatagramConnector(routes, run),
            name => variables.GetValueOrDefault(name),
            timeProvider ?? TimeProvider.System).ConfigureAwait(false);
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
