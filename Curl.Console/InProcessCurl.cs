using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Runs one curl command line in process, over connectors the caller supplies, for tools outside
/// the test projects that cannot reach <c>Curl.Console</c>'s internal types - such as the gap
/// office's file-based app <c>Gap/Tools/Measure-UpstreamCases.cs</c> (BL-1728, BL-1750).
/// It builds the same runner as <c>UpstreamConformanceTests.RunCurlAsync</c>: the progress meter
/// on, and every <c>%output{}</c> target of a <c>-w</c> template written to disk, with each line
/// feed as CR LF on Windows (ADR-0081).
/// </summary>
public static class InProcessCurl
{
    /// <summary>Runs <paramref name="arguments" /> as curl would and returns curl's exit code.</summary>
    /// <param name="arguments">The command line, without the program name.</param>
    /// <param name="standardOutput">Where curl writes its standard output.</param>
    /// <param name="standardError">Where curl writes its standard error.</param>
    /// <param name="standardInput">What curl reads as its standard input.</param>
    /// <param name="connector">Opens every TCP connection the transfers make.</param>
    /// <param name="datagramConnector">Opens every UDP channel the transfers make.</param>
    /// <returns>The exit code curl would end with.</returns>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null" />.</exception>
    public static Task<int> RunAsync(
        IReadOnlyList<string> arguments,
        Stream standardOutput,
        Stream standardError,
        Stream standardInput,
        IConnector connector,
        IDatagramConnector datagramConnector)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(standardOutput);
        ArgumentNullException.ThrowIfNull(standardError);
        ArgumentNullException.ThrowIfNull(standardInput);
        ArgumentNullException.ThrowIfNull(connector);
        ArgumentNullException.ThrowIfNull(datagramConnector);
        return CurlComposition.CreateRunner(
            standardOutput,
            standardError,
            standardInput,
            connector,
            datagramConnector,
            writesProgressMeter: true,
            writeOutFileOpener: new DiskWriteOutFileOpener(writesLineFeedAsCrLf: OperatingSystem.IsWindows())).RunAsync(arguments);
    }

    /// <summary>
    /// Runs <paramref name="arguments" /> as curl would over the production <see cref="TcpConnector" />,
    /// built from the command line as the executable builds it, with only the TCP dial and the name
    /// resolver injected, so a CONNECT tunnel, a HAProxy <c>PROXY</c> line, the <c>.onion</c> refusal
    /// and the <c>--resolve</c> and <c>--connect-to</c> entries reach the caller's server as they reach a
    /// real one (BL-1831). Returns curl's exit code.
    /// </summary>
    /// <param name="arguments">The command line, without the program name.</param>
    /// <param name="standardOutput">Where curl writes its standard output.</param>
    /// <param name="standardError">Where curl writes its standard error.</param>
    /// <param name="standardInput">What curl reads as its standard input.</param>
    /// <param name="tcpDialer">Opens every plaintext TCP connection the connector dials.</param>
    /// <param name="dnsResolver">Resolves every host name the connector looks up.</param>
    /// <param name="datagramConnector">Opens every UDP channel the transfers make.</param>
    /// <returns>The exit code curl would end with.</returns>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null" />.</exception>
    public static Task<int> RunAsync(
        IReadOnlyList<string> arguments,
        Stream standardOutput,
        Stream standardError,
        Stream standardInput,
        ITcpDialer tcpDialer,
        IDnsResolver dnsResolver,
        IDatagramConnector datagramConnector)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(standardOutput);
        ArgumentNullException.ThrowIfNull(standardError);
        ArgumentNullException.ThrowIfNull(standardInput);
        ArgumentNullException.ThrowIfNull(tcpDialer);
        ArgumentNullException.ThrowIfNull(dnsResolver);
        ArgumentNullException.ThrowIfNull(datagramConnector);
        return CurlComposition.CreateRunner(
            standardOutput,
            standardError,
            standardInput,
            tcpDialer,
            dnsResolver,
            datagramConnector,
            writesProgressMeter: true,
            writeOutFileOpener: new DiskWriteOutFileOpener(writesLineFeedAsCrLf: OperatingSystem.IsWindows())).RunAsync(arguments);
    }
}
