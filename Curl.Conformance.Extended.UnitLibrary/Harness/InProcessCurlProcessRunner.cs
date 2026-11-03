using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance.Extended.Harness;

/// <summary>
/// Runs one curl command line in process as <see cref="InProcessCurlRunner" /> does, with every program an
/// extension handler runs - an <c>mcp+stdio://</c> server, a <c>curl-proto-&lt;scheme&gt;</c> helper - started
/// by the given process starter and looked for in the environment it reads (BL-4577, ADR-1184):
/// <c>Curl.Console</c>'s <c>InProcessCurl.RunAsync</c> overload that takes an <see cref="IExtensionProcessStarter" />.
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
/// <param name="processStarter">Starts the programs the extension handlers run and reads the environment they find them in.</param>
/// <returns>curl's exit code.</returns>
public delegate Task<int> InProcessCurlProcessRunner(
    IReadOnlyList<string> arguments,
    Stream standardOutput,
    Stream standardError,
    Stream standardInput,
    ITcpDialer tcpDialer,
    IDnsResolver dnsResolver,
    IDatagramConnector datagramConnector,
    Func<string, string?> readEnvironmentVariable,
    TimeProvider timeProvider,
    IExtensionProcessStarter processStarter);
