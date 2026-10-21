using System.Net;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// An <see cref="IDnsResolver" /> that, for an <c>https://</c> origin, also asks for the origin's
/// HTTPS record (RFC 9460) before its addresses, as curl 8.22.0's HTTPSRR build does through
/// <c>--doh-url</c> (ADR-0727, BL-2920). <see cref="TcpConnector" /> calls it when it resolves a
/// direct connect's <c>https://</c> origin.
/// </summary>
public interface IHttpsOriginDnsResolver : IDnsResolver
{
    /// <summary>
    /// Asks for <paramref name="host" />'s HTTPS record for <paramref name="port" />, then resolves
    /// <paramref name="host" /> as <see cref="IDnsResolver.ResolveAsync" /> does.
    /// </summary>
    /// <param name="host">The origin's host name or literal address.</param>
    /// <param name="port">The port the transfer connects to, which names the HTTPS record asked for.</param>
    /// <param name="cancellationToken">Cancels the look-up.</param>
    /// <returns>The addresses for <paramref name="host" />, in the order they should be tried.</returns>
    ValueTask<IReadOnlyList<IPAddress>> ResolveHttpsOriginAsync(string host, int port, CancellationToken cancellationToken);
}
