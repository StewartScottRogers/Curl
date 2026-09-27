using System.Text;
using Curl.Authentication;
using Curl.Cli;
using Curl.Core;
using Curl.Core.FileSystem;
using Curl.Networking;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Dict;
using Curl.Protocol.File;
using Curl.Protocol.Gopher;
using Curl.Protocol.Http;
using Curl.Protocol.Mqtt;
using Curl.Protocol.Telnet;
using Curl.Protocol.Tftp;

namespace Curl.Console;

/// <summary>
/// The composition root: builds every service the executable needs with plain constructor
/// calls. There is no container, no reflection and no assembly scanning, so native
/// ahead-of-time publishing sees every type that is used.
/// </summary>
internal static class CurlComposition
{
    /// <summary>
    /// Creates the protocol handlers the executable registers: <c>file</c> over the real
    /// disk; <c>dict</c>, <c>gopher</c> and <c>gophers</c>, <c>telnet</c>, <c>mqtt</c>
    /// and <c>mqtts</c>, and <c>http</c> and <c>https</c> over <paramref name="connector" />,
    /// the last two answering authentication with <see cref="CreateHttpAuthenticator" />'s
    /// authenticator and keeping cookies in <paramref name="cookieStore" />; and <c>tftp</c> over
    /// <paramref name="datagramConnector" />. Each scheme is claimed by exactly one handler.
    /// </summary>
    /// <param name="connector">Connects the TCP protocols, with TLS for <c>gophers</c> and <c>mqtts</c>.</param>
    /// <param name="datagramConnector">Opens the UDP channels TFTP uses.</param>
    /// <param name="cookieStore">
    /// The cookies the HTTP handler sends and stores, or <see langword="null" /> to keep none.
    /// </param>
    /// <returns>Every registered handler.</returns>
    internal static IReadOnlyList<IProtocolHandler> CreateProtocolHandlers(
        IConnector connector,
        IDatagramConnector datagramConnector,
        ICookieStore? cookieStore = null) =>
        [
            new FileProtocolHandler(new PhysicalFileSystem()),
            new DictProtocolHandler(connector),
            new GopherProtocolHandler(connector),
            new TelnetProtocolHandler(connector),
            new TftpProtocolHandler(datagramConnector),
            new MqttProtocolHandler(connector),
            new HttpProtocolHandler(connector, CreateHttpAuthenticator(), cookieStore),
        ];

    /// <summary>
    /// Creates the HTTP authenticator: a <see cref="RankedHttpAuthenticator" /> that answers the
    /// scheme curl 8.21.0 picks among those <c>--basic</c>, <c>--digest</c> and <c>--anyauth</c>
    /// allow, with a <see cref="BasicAndBearerAuthenticator" /> for Basic, Bearer
    /// (<c>--oauth2-bearer</c>) and the first request, and a <see cref="DigestAuthenticator" />
    /// drawing each client nonce from <see cref="DigestClientNonce.CreateRandom" />; both encode
    /// credentials in the platform's encoding (<see cref="CredentialEncoding.ForPlatform" />).
    /// </summary>
    /// <returns>The authenticator.</returns>
    internal static RankedHttpAuthenticator CreateHttpAuthenticator()
    {
        Encoding credentialEncoding = CredentialEncoding.ForPlatform(OperatingSystem.IsWindows());

        return new RankedHttpAuthenticator(
            new BasicAndBearerAuthenticator(credentialEncoding),
            new DigestAuthenticator(credentialEncoding, DigestClientNonce.CreateRandom));
    }

    /// <summary>
    /// Creates the network transports for one run: a <see cref="TcpConnector" /> over a
    /// <see cref="SystemDnsResolver" />, a <see cref="TcpDialer" /> and an
    /// <see cref="SslStreamTlsProvider" />, and a <see cref="UdpDatagramConnector" />. Both
    /// connectors share the one resolver and <see cref="TimeProvider.System" />. TLS uses
    /// the <see cref="TlsClientOptions" /> mapped from <paramref name="options" /> by
    /// <see cref="TlsClientOptionsMapping.FromCommandLine" />, one set shared by every URL on
    /// the command line.
    /// </summary>
    /// <param name="options">The parsed command line.</param>
    /// <returns>The connectors and the pieces they were built from.</returns>
    internal static CurlTransports CreateTransports(CommandLineOptions options)
    {
        SystemDnsResolver dnsResolver = new();
        TimeProvider timeProvider = TimeProvider.System;
        TcpDialer tcpDialer = new();
        TlsClientOptions tlsClientOptions = TlsClientOptionsMapping.FromCommandLine(options);
        SslStreamTlsProvider tlsProvider = new(tlsClientOptions);

        return new CurlTransports(
            dnsResolver,
            timeProvider,
            tcpDialer,
            tlsClientOptions,
            tlsProvider,
            new TcpConnector(dnsResolver, tcpDialer, tlsProvider, timeProvider),
            new UdpDatagramConnector(dnsResolver, timeProvider));
    }

    /// <summary>
    /// Creates the runner that parses a command line and performs its transfers against
    /// the real disk, the real network and the given standard streams, wrapping warnings at
    /// the width <see cref="TerminalColumns.Resolve()" /> gives. The network
    /// transports are built by <see cref="CreateTransports(CommandLineOptions)" /> once the
    /// command line is parsed, because their TLS settings come from it.
    /// </summary>
    /// <param name="standardOutput">Where a transfer without <c>-o</c> writes its bytes.</param>
    /// <param name="standardError">Where the <c>curl: (N) message</c> lines go.</param>
    /// <param name="standardInput">What a <c>telnet</c> transfer sends to the server.</param>
    /// <param name="standardOutputIsTerminal">
    /// Whether standard output is a terminal, where the progress meter of a transfer with no
    /// <c>-o</c> is hidden, as curl hides it.
    /// </param>
    /// <returns>The runner, which writes curl's progress meter.</returns>
    internal static CurlCommandRunner CreateRunner(
        Stream standardOutput,
        Stream standardError,
        Stream standardInput,
        bool standardOutputIsTerminal) =>
        new(
            options => CreateTransferDispatch(CreateTransports(options), CookieEngine.FromCommandLine(options)),
            new PhysicalFileSystem(),
            new PhysicalFileSystem(),
            standardOutput,
            standardError,
            standardInput,
            OperatingSystem.IsWindows(),
            TerminalColumns.Resolve(),
            writesProgressMeter: true,
            standardOutputIsTerminal,
            outputPaths: new PhysicalOutputPaths());

    /// <summary>
    /// Creates the runner with the production handler set built around the given
    /// connectors instead of the real network, so the wiring can be checked without
    /// opening a socket.
    /// </summary>
    /// <param name="standardOutput">Where a transfer without <c>-o</c> writes its bytes.</param>
    /// <param name="standardError">Where the <c>curl: (N) message</c> lines go.</param>
    /// <param name="standardInput">What a <c>telnet</c> transfer sends to the server.</param>
    /// <param name="connector">Connects the TCP protocols.</param>
    /// <param name="datagramConnector">Opens the UDP channels TFTP uses.</param>
    /// <param name="proxySelector">
    /// Chooses each transfer's proxy, or <see langword="null" /> for one that reads no
    /// environment variables.
    /// </param>
    /// <returns>The runner.</returns>
    internal static CurlCommandRunner CreateRunner(
        Stream standardOutput,
        Stream standardError,
        Stream standardInput,
        IConnector connector,
        IDatagramConnector datagramConnector,
        ProxySelector? proxySelector = null) =>
        new(
            options => CreateTransferDispatch(connector, datagramConnector, CookieEngine.FromCommandLine(options), proxySelector),
            new PhysicalFileSystem(),
            new PhysicalFileSystem(),
            standardOutput,
            standardError,
            standardInput,
            OperatingSystem.IsWindows(),
            outputPaths: new PhysicalOutputPaths());

    /// <summary>
    /// Creates the dispatcher over the production handler set, connecting through
    /// <paramref name="transports" /> and keeping no cookies.
    /// </summary>
    /// <param name="transports">The run's connectors.</param>
    /// <returns>The dispatcher.</returns>
    internal static ProtocolDispatcher CreateDispatcher(CurlTransports transports) =>
        new(CreateProtocolHandlers(transports.TcpConnector, transports.UdpDatagramConnector));

    /// <summary>
    /// Creates what one run transfers through: the production handler set over
    /// <paramref name="transports" />, its HTTP handler keeping cookies in
    /// <paramref name="cookies" />; the TLS provider's <see cref="SslStreamTlsProvider.Warnings" />
    /// as the lines printed before each transfer, as curl 8.21.0 prints its <c>--capath</c>
    /// warnings once per URL; <paramref name="cookies" /> for the runner to load and save; and a
    /// <see cref="ProxySelector" /> reading the process's proxy environment variables.
    /// </summary>
    /// <param name="transports">The run's connectors.</param>
    /// <param name="cookies">The run's cookies, or <see langword="null" /> without <c>-b</c> or <c>-c</c>.</param>
    /// <returns>The dispatcher, the warning lines, the cookies and the proxy selector.</returns>
    internal static TransferDispatch CreateTransferDispatch(CurlTransports transports, CookieEngine? cookies = null) =>
        new(
            new ProtocolDispatcher(CreateProtocolHandlers(transports.TcpConnector, transports.UdpDatagramConnector, cookies?.HandlerStore)),
            transports.TlsProvider.Warnings,
            cookies,
            new ProxySelector(Environment.GetEnvironmentVariable));

    /// <summary>
    /// Creates what one run transfers through over the given connectors instead of the real
    /// network: the production handler set, its HTTP handler keeping cookies in
    /// <paramref name="cookies" />, no warning lines, <paramref name="cookies" /> and <paramref name="proxySelector" />.
    /// </summary>
    /// <param name="connector">Connects the TCP protocols.</param>
    /// <param name="datagramConnector">Opens the UDP channels TFTP uses.</param>
    /// <param name="cookies">The run's cookies, or <see langword="null" /> without <c>-b</c> or <c>-c</c>.</param>
    /// <param name="proxySelector">Chooses each transfer's proxy, or <see langword="null" /> for one that reads no environment variables.</param>
    /// <returns>The dispatcher, no warning lines, the cookies and the proxy selector.</returns>
    private static TransferDispatch CreateTransferDispatch(
        IConnector connector,
        IDatagramConnector datagramConnector,
        CookieEngine? cookies,
        ProxySelector? proxySelector) =>
        new(
            new ProtocolDispatcher(CreateProtocolHandlers(connector, datagramConnector, cookies?.HandlerStore)),
            [],
            cookies,
            proxySelector);
}
