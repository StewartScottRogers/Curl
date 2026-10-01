using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// What two <see cref="ConnectTarget" />s must share for a connection opened for one to
/// carry a request for the other (ADR-0050): the pool scheme, the host compared ignoring
/// case, the port, the TLS choice, whether the host is a forward proxy, the tunnelling proxy, the alt-svc
/// alternative dialled, and the configuration of the option group that asks.
/// </summary>
/// <remarks>
/// The host is compared as given, never as resolved, so <c>localhost</c> and
/// <c>127.0.0.1</c> are two keys, as curl 8.21.0 keeps them. The TLS options and the other
/// settings a connection is opened with are the <see cref="Configuration" /> of the
/// <see cref="PoolingConnector" /> asking: every <c>-:</c>/<c>--next</c> option group has its
/// own, and a connection is reused across groups only when they are equal (ADR-0285, BL-754).
/// </remarks>
/// <param name="Scheme">The target's <see cref="ConnectTarget.PoolScheme" />, lower-cased.</param>
/// <param name="Host">The target's host, upper-cased so the comparison ignores case.</param>
/// <param name="Port">The target's port.</param>
/// <param name="UseTls">The target's TLS choice.</param>
/// <param name="IsForwardProxy">The target's <see cref="ConnectTarget.IsForwardProxy" />.</param>
/// <param name="Proxy">The tunnelling proxy, or <see langword="null" /> for a direct connection.</param>
/// <param name="AltSvcAuthority">
/// The alternative the target dials (<see cref="ConnectTarget.AltSvcRoute" />) as upper-cased
/// <c>host:port</c>, or <see langword="null" /> when it dials the origin.
/// </param>
/// <param name="Configuration">
/// The asking <see cref="PoolingConnector" />'s configuration, compared with
/// <see cref="object.Equals(object?)" />, or <see langword="null" /> for none.
/// </param>
internal sealed record ConnectionPoolKey(
    string Scheme,
    string Host,
    int Port,
    bool UseTls,
    bool IsForwardProxy,
    ConnectionPoolProxyKey? Proxy,
    string? AltSvcAuthority,
    object? Configuration = null)
{
    /// <summary>
    /// Builds the key of <paramref name="target" />, or <see langword="null" /> when the
    /// target has no <see cref="ConnectTarget.PoolScheme" /> and so is never pooled.
    /// </summary>
    /// <param name="target">The target a connection is asked for.</param>
    /// <param name="configuration">The asking connector's configuration, or <see langword="null" /> for none.</param>
    /// <returns>The key, or <see langword="null" /> for a target that is never pooled.</returns>
    public static ConnectionPoolKey? Of(ConnectTarget target, object? configuration = null) =>
        target.PoolScheme is null
            ? null
            : new ConnectionPoolKey(
                target.PoolScheme.ToLowerInvariant(),
                target.Host.ToUpperInvariant(),
                target.Port,
                target.UseTls,
                target.IsForwardProxy,
                ConnectionPoolProxyKey.Of(target.Proxy),
                AltSvcAuthorityOf(target.AltSvcRoute),
                configuration);

    private static string? AltSvcAuthorityOf(AltSvcRoute? route) =>
        route is null ? null : $"{route.Alternative.Host.ToUpperInvariant()}:{route.Alternative.Port}";
}
