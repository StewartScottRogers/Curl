namespace Curl.Extension.Masque.Wire;

/// <summary>
/// What a MASQUE tunnel carries.
/// </summary>
internal enum MasqueTunnelKind
{
    /// <summary>UDP payloads to one target (RFC 9298, upgrade token <c>connect-udp</c>).</summary>
    ConnectUdp,

    /// <summary>Whole IP packets (RFC 9484, upgrade token <c>connect-ip</c>).</summary>
    ConnectIp,
}
