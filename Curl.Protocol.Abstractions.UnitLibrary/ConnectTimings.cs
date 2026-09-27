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
/// <see langword="null" /> when the host was a literal address and nothing was resolved.
/// </param>
/// <param name="Connected">
/// Taken when the TCP connect to the host or proxy completes, or for a tunnel when the
/// tunnel is open; the source of <c>%{time_connect}</c>.
/// </param>
/// <param name="TlsHandshakeCompleted">
/// Taken when the TLS handshake completes, the source of <c>%{time_appconnect}</c>;
/// <see langword="null" /> when <see cref="ConnectTarget.UseTls" /> is <see langword="false" />.
/// </param>
public sealed record ConnectTimings(
    long Started,
    long? NameResolved,
    long Connected,
    long? TlsHandshakeCompleted);
