using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance.Extended.Harness;

/// <summary>
/// Runs one curl command line in process over the given dialer, resolver, datagram connector and
/// environment, every timer of the run on the given clock, and returns its exit code: <c>Curl.Console</c>'s <c>InProcessCurl.RunAsync</c>, which
/// this library cannot reference, so the tests hand it in.
/// </summary>
/// <param name="arguments">The command line, without the program name.</param>
/// <param name="standardOutput">Where curl writes its standard output.</param>
/// <param name="standardError">Where curl writes its standard error.</param>
/// <param name="standardInput">What curl reads as its standard input.</param>
/// <param name="tcpDialer">Opens every TCP connection.</param>
/// <param name="dnsResolver">Resolves every host name.</param>
/// <param name="datagramConnector">Opens every UDP channel.</param>
/// <param name="readEnvironmentVariable">Returns an environment variable's value, or <see langword="null" /> when it is not set.</param>
/// <param name="timeProvider">The clock every timer of the run runs on, <c>-m</c>'s among them.</param>
/// <returns>curl's exit code.</returns>
public delegate Task<int> InProcessCurlRunner(
    IReadOnlyList<string> arguments,
    Stream standardOutput,
    Stream standardError,
    Stream standardInput,
    ITcpDialer tcpDialer,
    IDnsResolver dnsResolver,
    IDatagramConnector datagramConnector,
    Func<string, string?> readEnvironmentVariable,
    TimeProvider timeProvider);
