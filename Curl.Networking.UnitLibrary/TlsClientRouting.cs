namespace Curl.Networking;

/// <summary>
/// ADR-0140's routing rule as one pure function: a connection's TLS runs on the hand-built
/// client only when an option <c>SslStream</c> cannot honour is in force, and on
/// <c>SslStream</c> otherwise, so every transfer that works today keeps its bytes.
/// </summary>
/// <remarks>
/// Each row of ADR-0140's table is one condition here. Only the rows whose options reach
/// <see cref="TlsClientOptions" /> are present; the option tasks that carry the others
/// add a condition each; <c>--no-sessionid</c>'s and <c>--ssl-allow-beast</c>'s are ADR-0151's (BL-713):
/// <c>SslStream</c> can neither stop the system's session cache nor the TLS 1.0 CBC split; <c>--cert-status</c>'s row is ADR-0191's and <c>--curves</c> and
/// <c>--sigalgs</c>' is ADR-0151's (BL-709), and <c>--ssl-sessions</c>' is ADR-0319's (BL-710):
/// <c>SslStream</c> can neither export nor import a session, and <c>--ech</c>'s is ADR-0327's (BL-711): <c>SslStream</c> offers no
/// Encrypted Client Hello, and <c>--tls-earlydata</c>'s is BL-1105's: <c>SslStream</c> sends no 0-RTT early data. QUIC is not routed here: it has no
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

        return CapsVersionsBelowTls12(options) || options.RequireCertificateStatus || NamesGroupsOrSignatureAlgorithms(options) || UsesSessionsEchOrSrp(options)
            || ControlsSessionsEarlyDataOrTheBeastSplit(options)
            ? TlsClientRoute.HandBuilt
            : TlsClientRoute.SslStream;
    }

    // The --no-sessionid, --ssl-allow-beast (ADR-0151, BL-713) and --tls-earlydata (BL-1105) rows.
    private static bool ControlsSessionsEarlyDataOrTheBeastSplit(TlsClientOptions options) =>
        options.NoSessionId || AllowsBeastOnTls10(options) || options.AllowEarlyData;

    // The --ssl-allow-beast row (ADR-0151, BL-713): SslStream cannot turn off the TLS 1.0 CBC
    // split, so a range that reaches TLS 1.0 runs on the hand-built client. A TLS 1.0 or 1.1
    // ceiling is hand-built already; this adds a TLS 1.0 minimum under a higher one.
    private static bool AllowsBeastOnTls10(TlsClientOptions options) =>
        options.AllowBeast && options.MinimumVersion is TlsVersion.Tls10;

    // The --ssl-sessions, --ech and TLS-SRP rows: features SslStream has no API for.
    private static bool UsesSessionsEchOrSrp(TlsClientOptions options) =>
        options.SslSessionsFile is not null || EchModes.Of(options) != EchMode.Off || UsesTlsSrp(options);

    // The TLS-SRP row (ADR-0229, ADR-0328, BL-712): SslStream has no SRP, and --tlsuser alone
    // turns it on, as libcurl defaults the TLS authentication type to SRP once a user is set.
    private static bool UsesTlsSrp(TlsClientOptions options) => options.TlsUser is not null;

    // The --curves and --sigalgs row (ADR-0151, BL-709): SslStream offers the groups and
    // signature schemes the operating system chooses.
    private static bool NamesGroupsOrSignatureAlgorithms(TlsClientOptions options) =>
        options.Curves is not null || options.SignatureAlgorithms is not null;

    // The legacy-versions row: --tls-max 1.0 or 1.1, which the operating system's stack
    // refuses (ADR-0138 measured exit 35).
    private static bool CapsVersionsBelowTls12(TlsClientOptions options) =>
        options.MaximumVersion is TlsVersion.Tls10 or TlsVersion.Tls11;
}
