using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// What a <see cref="ManagedCurlEngine" /> built for the conformance harness dials and resolves through in place
/// of sockets, as <c>Curl.Console</c>'s dialing <c>CurlComposition.CreateRunner</c> takes them (ADR-0658): every
/// TCP dial goes through <see cref="TcpDialer" />, so the <see cref="TcpConnector" />'s proxy, TLS and connect code
/// stays in the path, every name is resolved by <see cref="DnsResolver" />, and every UDP and QUIC channel opens
/// through <see cref="DatagramConnector" />. Production engines have none.
/// </summary>
/// <param name="TcpDialer">Opens each plaintext TCP connection the transfers' <see cref="TcpConnector" /> dials.</param>
/// <param name="DnsResolver">Resolves every host name the transfers look up.</param>
/// <param name="DatagramConnector">Opens the datagram protocols' channels and HTTP/3's QUIC channels.</param>
internal sealed record EngineDialing(ITcpDialer TcpDialer, IDnsResolver DnsResolver, IDatagramConnector DatagramConnector);
