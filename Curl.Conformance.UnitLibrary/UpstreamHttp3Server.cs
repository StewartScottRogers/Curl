using System.Security.Cryptography.X509Certificates;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// What the case runner needs of the stand-in for upstream's <c>http/3</c> server, nghttpx-h3
/// serving HTTP/3 over QUIC in front of <c>sws</c>, which lives in
/// <c>Curl.Conformance.Http3Server.UnitLibrary</c> because it references this library and the QUIC,
/// TLS and HTTP/3 codecs this library does not: the caller builds it from there and hands it to
/// <see cref="UpstreamCaseRunner"/> (BL-2261), as it does <see cref="UpstreamHttp2Server"/>.
/// </summary>
/// <param name="CreateServer">
/// Makes a fresh server for one case in front of the given backend, the case's sws emulation, to
/// which it relays each HTTP/3 request as HTTP/1.1, presenting the given certificate,
/// <c>%CERTDIR/certs/test-localhost.pem</c>; datagrams to <see cref="Http3Port"/> reach it.
/// </param>
public sealed record UpstreamHttp3Server(Func<IConnector, X509Certificate2, IDatagramConnector> CreateServer)
{
    /// <summary>
    /// The value of <c>%HTTP3PORT</c>: servers.pm picks a free UDP port, and this is the next one no other
    /// stand-in uses.
    /// </summary>
    public const int Http3Port = 9019;

    /// <summary>
    /// Puts a fresh server on <see cref="Http3Port"/>, relaying to <paramref name="backend"/>, in front
    /// of the case's other datagram servers.
    /// </summary>
    /// <param name="otherPorts">Where every datagram open to another port goes.</param>
    /// <param name="backend">The HTTP/1.1 server the stand-in relays to, the case's sws emulation.</param>
    /// <param name="certificate">The certificate the stand-in presents, with its private key.</param>
    /// <returns>A datagram connector routing by port.</returns>
    public IDatagramConnector InFrontOf(IDatagramConnector otherPorts, IConnector backend, X509Certificate2 certificate) =>
        new Http3PortConnector(CreateServer(backend, certificate), otherPorts);

    /// <summary>
    /// Refuses a TCP connection to <see cref="Http3Port"/>, where the stand-in takes only datagrams, so
    /// <c>--http3</c>'s race of TCP against QUIC settles on QUIC, as upstream's cases expect, and passes every
    /// other connection on (ADR-0498).
    /// </summary>
    /// <param name="otherPorts">Where every connection to another port goes.</param>
    /// <returns>A connector routing by port.</returns>
    public static IConnector RefusingTcpOnHttp3Port(IConnector otherPorts) => new TcpPortRefusingConnector(otherPorts);

    private sealed class TcpPortRefusingConnector(IConnector otherPorts) : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            target.Port == Http3Port
                ? ValueTask.FromResult(ConnectResult.Refused($"Failed to connect to {target.Host}:{target.Port} after 0 ms: Could not connect to server"))
                : otherPorts.ConnectAsync(target, cancellationToken);
    }

    private sealed class Http3PortConnector(IDatagramConnector http3Server, IDatagramConnector otherPorts) : IDatagramConnector
    {
        public ValueTask<DatagramOpenResult> OpenAsync(string host, int port, CancellationToken cancellationToken) =>
            (port == Http3Port ? http3Server : otherPorts).OpenAsync(host, port, cancellationToken);
    }
}
