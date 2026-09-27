namespace Curl.Protocol.Abstractions;

/// <summary>
/// The points in time a connector records while it opens a connection, each a value
/// returned by <see cref="TimeProvider.GetTimestamp" /> on the connector's
/// <see cref="TimeProvider" />. A reader turns two of them into a duration with
/// <see cref="TimeProvider.GetElapsedTime(long, long)" /> on the same provider; see
/// ADR-0015.
/// </summary>
/// <param name="Started">
/// Taken when <see cref="IConnector.ConnectAsync(ConnectTarget, CancellationToken)" />
/// begins, before name resolution.
/// </param>
/// <param name="NameResolved">
/// Taken when name resolution finishes, the source of <c>%{time_namelookup}</c>;
/// <see langword="null" /> when the connector recorded none. <c>TcpConnector</c> sets it for a
/// literal address too, the moment the address was taken as resolved (ADR-0030).
/// </param>
/// <param name="Connected">
/// Taken when the TCP connect to the host or proxy completes, or for a tunnel when the
/// tunnel is open; the source of <c>%{time_connect}</c>. <see langword="null" /> when the
/// connect failed, so a refused dial reports <c>%{time_connect}</c> as <c>0</c> (ADR-0091).
/// </param>
/// <param name="TlsHandshakeCompleted">
/// Taken when the TLS handshake completes, the source of <c>%{time_appconnect}</c>;
/// <see langword="null" /> when <see cref="ConnectTarget.UseTls" /> is <see langword="false" />.
/// </param>
public sealed record ConnectTimings(
    long Started,
    long? NameResolved,
    long? Connected,
    long? TlsHandshakeCompleted);
