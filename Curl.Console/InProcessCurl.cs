using Curl.Cli;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Runs one curl command line in process, over connectors the caller supplies, for tools outside
/// the test projects that cannot reach <c>Curl.Console</c>'s internal types - such as the gap
/// office's file-based app <c>Gap/Tools/Measure-UpstreamCases.cs</c> (BL-1728, BL-1750).
/// It builds the same runner as <c>UpstreamConformanceTests.RunCurlAsync</c>: one run connection cache, so a later request
/// or URL reuses a kept-alive connection as the executable does (BL-1795), the progress meter
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
            securityContexts: CurlComposition.CreateDialingSecurityContextFactory(connector, datagramConnector, null, usesHandBuiltNtlm: true),
            runConnections: new ConnectionCache(TimeProvider.System),
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
        return CreateDialingRunner(standardOutput, standardError, standardInput, tcpDialer, dnsResolver, datagramConnector, readEnvironmentVariable: null, defaultConfigFileSearch: null, ftpListener: null).RunAsync(arguments);
    }

    /// <summary>
    /// Runs <paramref name="arguments" /> as curl would over the production <see cref="TcpConnector" />,
    /// as the overload without an environment does, but with <paramref name="readEnvironmentVariable" />
    /// as the whole environment the run reads - the proxy variables and <c>NO_PROXY</c>, <c>CURL_HOME</c>,
    /// <c>XDG_CONFIG_HOME</c> and <c>HOME</c> for the default config file, <c>IPFS_GATEWAY</c>,
    /// <c>IPFS_PATH</c> and <c>HOME/.ipfs/gateway</c> for the IPFS gateway, and the rest - so an upstream
    /// case's <c>&lt;setenv&gt;</c> reaches Curl without touching the calling process's environment
    /// (BL-1977). Returns curl's exit code.
    /// </summary>
    /// <param name="arguments">The command line, without the program name.</param>
    /// <param name="standardOutput">Where curl writes its standard output.</param>
    /// <param name="standardError">Where curl writes its standard error.</param>
    /// <param name="standardInput">What curl reads as its standard input.</param>
    /// <param name="tcpDialer">Opens every plaintext TCP connection the connector dials.</param>
    /// <param name="dnsResolver">Resolves every host name the connector looks up.</param>
    /// <param name="datagramConnector">Opens every UDP channel the transfers make.</param>
    /// <param name="readEnvironmentVariable">Returns an environment variable's value, or <see langword="null" /> when it is not set.</param>
    /// <returns>The exit code curl would end with.</returns>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null" />.</exception>
    public static Task<int> RunAsync(
        IReadOnlyList<string> arguments,
        Stream standardOutput,
        Stream standardError,
        Stream standardInput,
        ITcpDialer tcpDialer,
        IDnsResolver dnsResolver,
        IDatagramConnector datagramConnector,
        Func<string, string?> readEnvironmentVariable)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(standardOutput);
        ArgumentNullException.ThrowIfNull(standardError);
        ArgumentNullException.ThrowIfNull(standardInput);
        ArgumentNullException.ThrowIfNull(tcpDialer);
        ArgumentNullException.ThrowIfNull(dnsResolver);
        ArgumentNullException.ThrowIfNull(datagramConnector);
        ArgumentNullException.ThrowIfNull(readEnvironmentVariable);
        return RunWithEnvironmentAsync(arguments, standardOutput, standardError, standardInput, tcpDialer, dnsResolver, datagramConnector, readEnvironmentVariable, ftpListener: null);
    }

    /// <summary>
    /// Runs <paramref name="arguments" /> as the overload with an environment does, with
    /// <paramref name="ftpListener" /> listening for FTP's active-mode (<c>-P</c>) data connections in
    /// place of a real TCP listener, so the server a <c>PORT</c> or <c>EPRT</c> names connects to the
    /// port curl announced, as <c>UpstreamConformanceTests.RunCurlAsync</c> runs upstream's
    /// active-mode cases (BL-1978). Returns curl's exit code.
    /// </summary>
    /// <param name="arguments">The command line, without the program name.</param>
    /// <param name="standardOutput">Where curl writes its standard output.</param>
    /// <param name="standardError">Where curl writes its standard error.</param>
    /// <param name="standardInput">What curl reads as its standard input.</param>
    /// <param name="tcpDialer">Opens every plaintext TCP connection the connector dials.</param>
    /// <param name="dnsResolver">Resolves every host name the connector looks up.</param>
    /// <param name="datagramConnector">Opens every UDP channel the transfers make.</param>
    /// <param name="readEnvironmentVariable">Returns an environment variable's value, or <see langword="null" /> when it is not set.</param>
    /// <param name="ftpListener">Listens for FTP's active-mode data connections.</param>
    /// <returns>The exit code curl would end with.</returns>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null" />.</exception>
    public static Task<int> RunAsync(
        IReadOnlyList<string> arguments,
        Stream standardOutput,
        Stream standardError,
        Stream standardInput,
        ITcpDialer tcpDialer,
        IDnsResolver dnsResolver,
        IDatagramConnector datagramConnector,
        Func<string, string?> readEnvironmentVariable,
        IConnectionListener ftpListener)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(standardOutput);
        ArgumentNullException.ThrowIfNull(standardError);
        ArgumentNullException.ThrowIfNull(standardInput);
        ArgumentNullException.ThrowIfNull(tcpDialer);
        ArgumentNullException.ThrowIfNull(dnsResolver);
        ArgumentNullException.ThrowIfNull(datagramConnector);
        ArgumentNullException.ThrowIfNull(readEnvironmentVariable);
        ArgumentNullException.ThrowIfNull(ftpListener);
        return RunWithEnvironmentAsync(arguments, standardOutput, standardError, standardInput, tcpDialer, dnsResolver, datagramConnector, readEnvironmentVariable, ftpListener);
    }

    /// <summary>
    /// Runs the dialing runner with <paramref name="readEnvironmentVariable" /> as the whole
    /// environment, the default config file looked for only where it points.
    /// </summary>
    private static Task<int> RunWithEnvironmentAsync(
        IReadOnlyList<string> arguments,
        Stream standardOutput,
        Stream standardError,
        Stream standardInput,
        ITcpDialer tcpDialer,
        IDnsResolver dnsResolver,
        IDatagramConnector datagramConnector,
        Func<string, string?> readEnvironmentVariable,
        IConnectionListener? ftpListener)
    {
        DefaultConfigFileSearch configFileSearch = new(readEnvironmentVariable, OperatingSystem.IsWindows(), executableDirectory: null, accountHomeDirectory: null);
        return CreateDialingRunner(standardOutput, standardError, standardInput, tcpDialer, dnsResolver, datagramConnector, readEnvironmentVariable, configFileSearch, ftpListener).RunAsync(arguments);
    }

    /// <summary>
    /// Builds the dialing runner every dialing overload runs: the progress meter on, and the <c>-w</c>
    /// <c>%output{}</c> targets written to disk with CR LF line feeds on Windows.
    /// </summary>
    private static CurlCommandRunner CreateDialingRunner(
        Stream standardOutput,
        Stream standardError,
        Stream standardInput,
        ITcpDialer tcpDialer,
        IDnsResolver dnsResolver,
        IDatagramConnector datagramConnector,
        Func<string, string?>? readEnvironmentVariable,
        DefaultConfigFileSearch? defaultConfigFileSearch,
        IConnectionListener? ftpListener) =>
        CurlComposition.CreateRunner(
            standardOutput,
            standardError,
            standardInput,
            tcpDialer,
            dnsResolver,
            datagramConnector,
            writesProgressMeter: true,
            writeOutFileOpener: new DiskWriteOutFileOpener(writesLineFeedAsCrLf: OperatingSystem.IsWindows()),
            usesHandBuiltNtlm: true,
            readEnvironmentVariable: readEnvironmentVariable,
            defaultConfigFileSearch: defaultConfigFileSearch,
            ftpListener: ftpListener);
}
