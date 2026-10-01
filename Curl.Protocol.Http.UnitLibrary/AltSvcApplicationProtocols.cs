using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// The ALPN list a TLS connect to an Alt-Svc alternative offers in place of the connector's own
/// (<see cref="ConnectTarget.ApplicationProtocols" />), as curl 8.21.0 offers it (measured, BL-733
/// Notes, BL-948, ADR-0226).
/// </summary>
internal static class AltSvcApplicationProtocols
{
    private static readonly IReadOnlyList<string> H2Only = ["h2"];

    private static readonly IReadOnlyList<string> Http11Only = ["http/1.1"];

    /// <summary>
    /// Gives what a connect over <paramref name="route" /> offers through ALPN: <c>h2</c> alone when
    /// the alternative switches to <c>h2</c> and <c>http/1.1</c> alone when it switches to <c>h1</c>;
    /// <see langword="null" />, so the connector offers its own list, with no route, for an alternative
    /// of the version it was found under (where curl offers the version option's list, measured), and
    /// for an <c>h3</c> one, which QUIC carries.
    /// </summary>
    /// <param name="route">The transfer's route, or <see langword="null" /> for none.</param>
    /// <returns>The list, or <see langword="null" /> for the connector's own.</returns>
    internal static IReadOnlyList<string>? Of(AltSvcRoute? route) =>
        route is null || route.OriginAlpn == route.Alternative.Alpn
            ? null
            : route.Alternative.Alpn switch
            {
                "h2" => H2Only,
                "h1" => Http11Only,
                _ => null,
            };
}
