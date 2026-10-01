using System.Net;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// What <see cref="TcpConnector" /> hands <see cref="QuicDialer" /> once the host has resolved:
/// the target, where it resolved to, when the connect began and resolved, the connect timeout,
/// the number the connection is reported with and what its UDP sockets bind to.
/// </summary>
/// <param name="Target">The target as the URL names it; its host is the one TLS verifies and curl's failure lines name.</param>
/// <param name="DestinationHost">The host dialled, which a <c>--connect-to</c> mapping may have changed.</param>
/// <param name="Port">The port dialled, which a <c>--connect-to</c> mapping may have changed.</param>
/// <param name="Addresses">The resolved addresses, in the order they are tried; never empty.</param>
/// <param name="Started">The timestamp the connect began at.</param>
/// <param name="NameResolved">The timestamp the host resolved at.</param>
/// <param name="ConnectTimeout">The <c>--connect-timeout</c>, or <see langword="null" /> for QUIC's 10-second handshake timeout.</param>
/// <param name="ConnectionNumber">The number the connection is reported with.</param>
/// <param name="LocalBinding">
/// Chooses the local address each UDP socket binds and gives its ports (<c>--interface</c>, <c>--local-port</c>,
/// BL-1025), or <see langword="null" /> to bind any address on an ephemeral port.
/// </param>
internal sealed record QuicDialRequest(
    ConnectTarget Target,
    string DestinationHost,
    int Port,
    IReadOnlyList<IPAddress> Addresses,
    long Started,
    long NameResolved,
    TimeSpan? ConnectTimeout,
    long ConnectionNumber,
    LocalBindingAddressChooser? LocalBinding = null);
