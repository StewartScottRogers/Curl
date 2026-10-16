namespace Curl.Api.Callbacks;

/// <summary>
/// The address an open-socket callback is asked to create a socket for: libcurl's
/// <c>struct curl_sockaddr</c>.
/// </summary>
/// <param name="Family">The address family (<c>AF_INET</c>, <c>AF_INET6</c>, <c>AF_UNIX</c>), as the platform numbers it.</param>
/// <param name="SocketType">The socket type (<c>SOCK_STREAM</c>, <c>SOCK_DGRAM</c>), as the platform numbers it.</param>
/// <param name="Protocol">The protocol (<c>IPPROTO_TCP</c>, <c>IPPROTO_UDP</c>), as the platform numbers it.</param>
/// <param name="Address">The raw <c>sockaddr</c> bytes to connect to.</param>
public sealed record CurlSocketAddress(int Family, int SocketType, int Protocol, ReadOnlyMemory<byte> Address);
