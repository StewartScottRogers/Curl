using Curl.Core;
using Curl.Core.FileSystem;
using Curl.Networking;
using Curl.Protocol.Abstractions;
using Curl.Protocol.File;

namespace Curl.Console;

/// <summary>
/// The composition root: builds every service the executable needs with plain constructor
/// calls. There is no container, no reflection and no assembly scanning, so native
/// ahead-of-time publishing sees every type that is used.
/// </summary>
internal static class CurlComposition
{
    /// <summary>
    /// Creates the protocol handlers the executable registers. Today that is the
    /// <c>file</c> handler only.
    /// </summary>
    /// <returns>Every registered handler.</returns>
    internal static IReadOnlyList<IProtocolHandler> CreateProtocolHandlers() =>
        [new FileProtocolHandler(new PhysicalFileSystem())];

    /// <summary>
    /// Creates the network transports for one run: a <see cref="TcpConnector" /> over a
    /// <see cref="SystemDnsResolver" />, a <see cref="TcpDialer" /> and an
    /// <see cref="SslStreamTlsProvider" />, and a <see cref="UdpDatagramConnector" />. Both
    /// connectors share the one resolver and <see cref="TimeProvider.System" />. TLS uses
    /// <see cref="TlsClientOptions" />'s defaults: certificates are verified and the
    /// version is left to the operating system.
    /// </summary>
    /// <returns>The connectors and the pieces they were built from.</returns>
    internal static CurlTransports CreateTransports()
    {
        SystemDnsResolver dnsResolver = new();
        TimeProvider timeProvider = TimeProvider.System;
        TcpDialer tcpDialer = new();
        TlsClientOptions tlsClientOptions = new();
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
    /// the real disk and the given standard streams.
    /// </summary>
    /// <param name="standardOutput">Where a transfer without <c>-o</c> writes its bytes.</param>
    /// <param name="standardError">Where the <c>curl: (N) message</c> lines go.</param>
    /// <returns>The runner.</returns>
    internal static CurlCommandRunner CreateRunner(Stream standardOutput, Stream standardError) =>
        new(
            new ProtocolDispatcher(CreateProtocolHandlers()),
            new PhysicalFileSystem(),
            standardOutput,
            standardError);
}
