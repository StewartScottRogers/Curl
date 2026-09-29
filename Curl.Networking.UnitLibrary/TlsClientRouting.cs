namespace Curl.Networking;

/// <summary>
/// ADR-0140's routing rule as one pure function: a connection's TLS runs on the hand-built
/// client only when an option <c>SslStream</c> cannot honour is in force, and on
/// <c>SslStream</c> otherwise, so every transfer that works today keeps its bytes.
/// </summary>
/// <remarks>
/// Each row of ADR-0140's table is one condition here. Only the rows whose options reach
/// <see cref="TlsClientOptions" /> are present; the option tasks that carry the others
/// (BL-618, BL-610, BL-713) add a condition each. QUIC is not routed here: it has no
/// <c>SslStream</c> route at all.
/// </remarks>
public static class TlsClientRouting
{
    /// <summary>Chooses the TLS client for a connection made with <paramref name="options" />.</summary>
    /// <param name="options">The connection's TLS options, the origin's or an HTTPS proxy's.</param>
    /// <returns>
    /// <see cref="TlsClientRoute.HandBuilt" /> when a row of ADR-0140's table holds, otherwise
    /// <see cref="TlsClientRoute.SslStream" />.
    /// </returns>
    public static TlsClientRoute Choose(TlsClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return CapsVersionsBelowTls12(options) ? TlsClientRoute.HandBuilt : TlsClientRoute.SslStream;
    }

    // The legacy-versions row: --tls-max 1.0 or 1.1, which the operating system's stack
    // refuses (ADR-0138 measured exit 35).
    private static bool CapsVersionsBelowTls12(TlsClientOptions options) =>
        options.MaximumVersion is TlsVersion.Tls10 or TlsVersion.Tls11;
}
