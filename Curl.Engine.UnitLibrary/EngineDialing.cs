using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// What a <see cref="ManagedCurlEngine" /> built for the conformance harness dials and resolves through in place
/// of sockets, as <c>Curl.Console</c>'s dialing <c>CurlComposition.CreateRunner</c> takes them (ADR-0658): every
/// TCP dial goes through <see cref="TcpDialer" />, so the <see cref="TcpConnector" />'s proxy, TLS and connect code
/// stays in the path, every name is resolved by <see cref="DnsResolver" />, every UDP and QUIC channel opens
/// through <see cref="DatagramConnector" />, and FTP's active-mode data connections arrive through
/// <see cref="FtpListener" />. Production engines have none.
/// </summary>
/// <param name="TcpDialer">Opens each plaintext TCP connection the transfers' <see cref="TcpConnector" /> dials.</param>
/// <param name="DnsResolver">Resolves every host name the transfers look up.</param>
/// <param name="DatagramConnector">Opens the datagram protocols' channels and HTTP/3's QUIC channels.</param>
/// <param name="FtpListener">
/// Listens for FTP's active-mode (<c>CURLOPT_FTPPORT</c>) data connections, as the dialing runner's <c>ftpListener</c>
/// does; <see langword="null" /> for a <see cref="TcpConnectionListener" />.
/// </param>
internal sealed record EngineDialing(ITcpDialer TcpDialer, IDnsResolver DnsResolver, IDatagramConnector DatagramConnector, IConnectionListener? FtpListener = null);
