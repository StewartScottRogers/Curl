using System.Net;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// An <see cref="IDnsResolver" /> that, for an <c>https://</c> origin, also asks for the origin's
/// HTTPS record (RFC 9460) before its addresses, as curl 8.22.0's HTTPSRR build does through
/// <c>--doh-url</c> (ADR-0727, BL-2920). <see cref="TcpConnector" /> calls it when it resolves a
/// direct connect's <c>https://</c> origin under <see cref="TcpConnector.AsksHttpsRecords" />, off on Windows (ADR-0856).
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

    /// <summary>
    /// Gives the HTTPS record <see cref="ResolveHttpsOriginAsync" /> found for <paramref name="host" />
    /// and <paramref name="port" />, which <see cref="TcpConnector" /> reports as curl 8.22.0's
    /// <c>HTTPS-RR &lt;host&gt;:&lt;port&gt;:</c> line (ADR-0727's BL-2980 amendment).
    /// </summary>
    /// <param name="host">The origin's host name, as given to <see cref="ResolveHttpsOriginAsync" />.</param>
    /// <param name="port">The port, as given to <see cref="ResolveHttpsOriginAsync" />.</param>
    /// <returns>The first record the answer held, decoded; <see langword="null" /> when there was none or nothing was asked.</returns>
    ServiceBindingRecord? HttpsRecordFor(string host, int port);
}
