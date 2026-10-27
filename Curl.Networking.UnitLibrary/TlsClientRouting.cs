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
/// Encrypted Client Hello, and <c>--tls-earlydata</c>'s is BL-1105's: <c>SslStream</c> sends no 0-RTT early data, and a TLS 1.0 or 1.1 minimum's is ADR-0360's (BL-1143): an operating-system stack may refuse those versions, and <c>SSLKEYLOGFILE</c>'s is BL-2327's: <c>SslStream</c> cannot log its secrets. QUIC is not routed here: it has no
/// <c>SslStream</c> route at all.
/// </remarks>
public static class TlsClientRouting
{
    // ADR-0140's table, one row per condition, in the order the reason is looked for: the
    // first row that holds names why the hand-built client was chosen (BL-968).
    private static readonly (Func<TlsClientOptions, bool> Holds, string Reason)[] HandBuiltRows =
    [
        (CapsVersionsBelowTls12, "--tls-max caps the versions below TLS 1.2"),
        (options => options.RequireCertificateStatus, "--cert-status asks for the stapled certificate status"),
        (NamesGroupsOrSignatureAlgorithms, "--curves or --sigalgs names the groups or signature algorithms"),
        (options => options.SslSessionsFile is not null, "--ssl-sessions imports and exports sessions"),
        (options => EchModes.Of(options) != EchMode.Off, "--ech offers Encrypted Client Hello"),
        (options => options.NoSessionId, "--no-sessionid turns off the session cache"),
        (AllowsBeastOnTls10, "--ssl-allow-beast with a TLS 1.0 minimum turns off the CBC split"),
        (options => options.AllowEarlyData, "--tls-earlydata sends 0-RTT early data"),
        (LetsVersionsReachBelowTls12, "--tlsv1.0 or --tlsv1.1 lets the versions reach below TLS 1.2"),
        (options => options.KeyLogFile is not null, "SSLKEYLOGFILE logs the TLS secrets"),
    ];

    /// <summary>Chooses the TLS client for a connection made with <paramref name="options" />.</summary>
    /// <param name="options">The connection's TLS options, the origin's or an HTTPS proxy's.</param>
    /// <returns>
    /// <see cref="TlsClientRoute.HandBuilt" /> when a row of ADR-0140's table holds, otherwise
    /// <see cref="TlsClientRoute.SslStream" />.
    /// </returns>
    public static TlsClientRoute Choose(TlsClientOptions options) =>
        Reason(options) is null ? TlsClientRoute.SslStream : TlsClientRoute.HandBuilt;

    /// <summary>
    /// Says why <see cref="Choose" /> chooses the hand-built client for
    /// <paramref name="options" />, naming the option of the first row of ADR-0140's table that
    /// holds, for the diagnostic log's handshake line (BL-968).
    /// </summary>
    /// <param name="options">The connection's TLS options.</param>
    /// <returns>The reason, or <see langword="null" /> when no row holds and <c>SslStream</c> runs the handshake.</returns>
    public static string? Reason(TlsClientOptions options) => ReasonInBuild(options, OperatingSystem.IsWindows());

    /// <summary>
    /// Says why the hand-built client is chosen for <paramref name="options" /> in the named build,
    /// as <see cref="Reason(TlsClientOptions)" /> does for the build the platform usually runs.
    /// </summary>
    /// <param name="options">The connection's TLS options.</param>
    /// <param name="matchesSchannelBuild">Whether this is the Schannel build.</param>
    /// <returns>The reason, or <see langword="null" /> when no row holds and <c>SslStream</c> runs the handshake.</returns>
    internal static string? ReasonInBuild(TlsClientOptions options, bool matchesSchannelBuild)
    {
        ArgumentNullException.ThrowIfNull(options);

        return HandBuiltRows.FirstOrDefault(row => row.Holds(options)).Reason
            ?? (ChecksCaCertificateFileRevocationOnSchannel(options, matchesSchannelBuild) ? StapledRevocationReason : null);
    }

    private const string StapledRevocationReason = "the Schannel build checks the --cacert chain's revocation, which a stapled OCSP response answers";

    // The stapled-revocation row (ADR-0834, CGR-014): curl's Schannel build checks a --cacert
    // chain's revocation against the OCSP response the server stapled, which SslStream never
    // hands over, so that check runs on the hand-built client, whose hello asks for the staple.
    private static bool ChecksCaCertificateFileRevocationOnSchannel(TlsClientOptions options, bool matchesSchannelBuild) =>
        matchesSchannelBuild && options.HasCaCertificates && !options.SkipRevocationCheck && !options.Insecure;

    // The --ssl-allow-beast row (ADR-0151, BL-713): SslStream cannot turn off the TLS 1.0 CBC
    // split, so a range that reaches TLS 1.0 runs on the hand-built client. A TLS 1.0 or 1.1
    // ceiling is hand-built already; this adds a TLS 1.0 minimum under a higher one.
    private static bool AllowsBeastOnTls10(TlsClientOptions options) =>
        options.AllowBeast && options.MinimumVersion is TlsVersion.Tls10;

    // The --curves and --sigalgs row (ADR-0151, BL-709): SslStream offers the groups and
    // signature schemes the operating system chooses.
    private static bool NamesGroupsOrSignatureAlgorithms(TlsClientOptions options) =>
        options.Curves is not null || options.SignatureAlgorithms is not null;

    // The legacy-minimum row (ADR-0360, BL-1143): --tlsv1.0 or --tlsv1.1 with no legacy ceiling,
    // which the hand-built client offers from TLS 1.3 down, so a TLS 1.0 or 1.1 server connects even
    // where the operating system's stack refuses those versions, as curl with Schannel does.
    private static bool LetsVersionsReachBelowTls12(TlsClientOptions options) =>
        options.MinimumVersion is TlsVersion.Tls10 or TlsVersion.Tls11;

    // The legacy-versions row: --tls-max 1.0 or 1.1, which the operating system's stack
    // refuses (ADR-0138 measured exit 35).
    private static bool CapsVersionsBelowTls12(TlsClientOptions options) =>
        options.MaximumVersion is TlsVersion.Tls10 or TlsVersion.Tls11;
}
