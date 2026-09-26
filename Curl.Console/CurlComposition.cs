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
    /// the last two answering authentication with a <see cref="BasicAndBearerAuthenticator" />
    /// in the platform's credential encoding and keeping no cookies; and <c>tftp</c> over
    /// <paramref name="datagramConnector" />. Each scheme is claimed by exactly one handler.
    /// </summary>
    /// <param name="connector">Connects the TCP protocols, with TLS for <c>gophers</c> and <c>mqtts</c>.</param>
    /// <param name="datagramConnector">Opens the UDP channels TFTP uses.</param>
    /// <returns>Every registered handler.</returns>
    internal static IReadOnlyList<IProtocolHandler> CreateProtocolHandlers(
        IConnector connector,
        IDatagramConnector datagramConnector) =>
        [
            new FileProtocolHandler(new PhysicalFileSystem()),
            new DictProtocolHandler(connector),
            new GopherProtocolHandler(connector),
            new TelnetProtocolHandler(connector),
            new TftpProtocolHandler(datagramConnector),
            new MqttProtocolHandler(connector),
            new HttpProtocolHandler(
                connector,
                new BasicAndBearerAuthenticator(CredentialEncoding.ForPlatform(OperatingSystem.IsWindows()))),
        ];

    /// <summary>
    /// Creates the network transports for one run: a <see cref="TcpConnector" /> over a
    /// <see cref="SystemDnsResolver" />, a <see cref="TcpDialer" /> and an
    /// <see cref="SslStreamTlsProvider" />, and a <see cref="UdpDatagramConnector" />. Both
    /// connectors share the one resolver and <see cref="TimeProvider.System" />. TLS uses
    /// the <see cref="TlsClientOptions" /> mapped from <paramref name="options" />'s
    /// <c>-k</c>, <c>--cacert</c>, <c>--tlsv1.2</c> and <c>--tlsv1.3</c>, one set shared
    /// by every URL on the command line.
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
            options => CreateDispatcher(CreateTransports(options)),
            new PhysicalFileSystem(),
            new PhysicalFileSystem(),
            standardOutput,
            standardError,
            standardInput,
            OperatingSystem.IsWindows(),
            TerminalColumns.Resolve(),
            writesProgressMeter: true,
            standardOutputIsTerminal);

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
    /// <returns>The runner.</returns>
    internal static CurlCommandRunner CreateRunner(
        Stream standardOutput,
        Stream standardError,
        Stream standardInput,
        IConnector connector,
        IDatagramConnector datagramConnector) =>
        new(
            _ => new ProtocolDispatcher(CreateProtocolHandlers(connector, datagramConnector)),
            new PhysicalFileSystem(),
            new PhysicalFileSystem(),
            standardOutput,
            standardError,
            standardInput,
            OperatingSystem.IsWindows());

    /// <summary>
    /// Creates the dispatcher over the production handler set, connecting through
    /// <paramref name="transports" />.
    /// </summary>
    /// <param name="transports">The run's connectors.</param>
    /// <returns>The dispatcher.</returns>
    internal static ProtocolDispatcher CreateDispatcher(CurlTransports transports) =>
        new(CreateProtocolHandlers(transports.TcpConnector, transports.UdpDatagramConnector));
}
