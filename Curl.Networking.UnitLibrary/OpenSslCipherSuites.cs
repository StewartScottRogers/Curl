using System.Collections.Frozen;
using System.Net.Security;

namespace Curl.Networking;

/// <summary>
/// Turns <c>--ciphers</c> and <c>--tls13-ciphers</c> into the one list of suites
/// <see cref="CipherSuitesPolicy" /> takes, as ADR-0011 decides for the OpenSSL build of
/// curl: entries are split on <c>:</c>, <c>,</c> and spaces, matched case-sensitively
/// against the IANA name and then the OpenSSL name, and unknown entries are dropped; a list
/// with no known entry is exit 59.
/// </summary>
internal static class OpenSslCipherSuites
{
    // OpenSSL's names for the TLS 1.2-and-below suites TlsCipherSuite has, in the order
    // they are offered when --ciphers is absent.
    private static readonly (string OpenSslName, TlsCipherSuite Suite)[] Tls12Names =
    [
        ("ECDHE-ECDSA-AES256-GCM-SHA384", TlsCipherSuite.TLS_ECDHE_ECDSA_WITH_AES_256_GCM_SHA384),
        ("ECDHE-RSA-AES256-GCM-SHA384", TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384),
        ("DHE-RSA-AES256-GCM-SHA384", TlsCipherSuite.TLS_DHE_RSA_WITH_AES_256_GCM_SHA384),
        ("ECDHE-ECDSA-CHACHA20-POLY1305", TlsCipherSuite.TLS_ECDHE_ECDSA_WITH_CHACHA20_POLY1305_SHA256),
        ("ECDHE-RSA-CHACHA20-POLY1305", TlsCipherSuite.TLS_ECDHE_RSA_WITH_CHACHA20_POLY1305_SHA256),
        ("DHE-RSA-CHACHA20-POLY1305", TlsCipherSuite.TLS_DHE_RSA_WITH_CHACHA20_POLY1305_SHA256),
        ("ECDHE-ECDSA-AES128-GCM-SHA256", TlsCipherSuite.TLS_ECDHE_ECDSA_WITH_AES_128_GCM_SHA256),
        ("ECDHE-RSA-AES128-GCM-SHA256", TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256),
        ("DHE-RSA-AES128-GCM-SHA256", TlsCipherSuite.TLS_DHE_RSA_WITH_AES_128_GCM_SHA256),
        ("ECDHE-ECDSA-AES256-SHA384", TlsCipherSuite.TLS_ECDHE_ECDSA_WITH_AES_256_CBC_SHA384),
        ("ECDHE-RSA-AES256-SHA384", TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_256_CBC_SHA384),
        ("DHE-RSA-AES256-SHA256", TlsCipherSuite.TLS_DHE_RSA_WITH_AES_256_CBC_SHA256),
        ("ECDHE-ECDSA-AES128-SHA256", TlsCipherSuite.TLS_ECDHE_ECDSA_WITH_AES_128_CBC_SHA256),
        ("ECDHE-RSA-AES128-SHA256", TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_128_CBC_SHA256),
        ("DHE-RSA-AES128-SHA256", TlsCipherSuite.TLS_DHE_RSA_WITH_AES_128_CBC_SHA256),
        ("ECDHE-ECDSA-AES256-SHA", TlsCipherSuite.TLS_ECDHE_ECDSA_WITH_AES_256_CBC_SHA),
        ("ECDHE-RSA-AES256-SHA", TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_256_CBC_SHA),
        ("DHE-RSA-AES256-SHA", TlsCipherSuite.TLS_DHE_RSA_WITH_AES_256_CBC_SHA),
        ("ECDHE-ECDSA-AES128-SHA", TlsCipherSuite.TLS_ECDHE_ECDSA_WITH_AES_128_CBC_SHA),
        ("ECDHE-RSA-AES128-SHA", TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_128_CBC_SHA),
        ("DHE-RSA-AES128-SHA", TlsCipherSuite.TLS_DHE_RSA_WITH_AES_128_CBC_SHA),
        ("AES256-GCM-SHA384", TlsCipherSuite.TLS_RSA_WITH_AES_256_GCM_SHA384),
        ("AES128-GCM-SHA256", TlsCipherSuite.TLS_RSA_WITH_AES_128_GCM_SHA256),
        ("AES256-SHA256", TlsCipherSuite.TLS_RSA_WITH_AES_256_CBC_SHA256),
        ("AES128-SHA256", TlsCipherSuite.TLS_RSA_WITH_AES_128_CBC_SHA256),
        ("AES256-SHA", TlsCipherSuite.TLS_RSA_WITH_AES_256_CBC_SHA),
        ("AES128-SHA", TlsCipherSuite.TLS_RSA_WITH_AES_128_CBC_SHA),
    ];

    // OpenSSL's names for the DHE-DSS suites (openssl ciphers -V): offered only when
    // --ciphers names them, since OpenSSL's DEFAULT list and its measured hello have none.
    private static readonly (string OpenSslName, TlsCipherSuite Suite)[] Tls12DssNames =
    [
        ("DHE-DSS-AES256-GCM-SHA384", TlsCipherSuite.TLS_DHE_DSS_WITH_AES_256_GCM_SHA384),
        ("DHE-DSS-ARIA256-GCM-SHA384", TlsCipherSuite.TLS_DHE_DSS_WITH_ARIA_256_GCM_SHA384),
        ("DHE-DSS-AES128-GCM-SHA256", TlsCipherSuite.TLS_DHE_DSS_WITH_AES_128_GCM_SHA256),
        ("DHE-DSS-ARIA128-GCM-SHA256", TlsCipherSuite.TLS_DHE_DSS_WITH_ARIA_128_GCM_SHA256),
        ("DHE-DSS-AES256-SHA256", TlsCipherSuite.TLS_DHE_DSS_WITH_AES_256_CBC_SHA256),
        ("DHE-DSS-CAMELLIA256-SHA256", TlsCipherSuite.TLS_DHE_DSS_WITH_CAMELLIA_256_CBC_SHA256),
        ("DHE-DSS-AES128-SHA256", TlsCipherSuite.TLS_DHE_DSS_WITH_AES_128_CBC_SHA256),
        ("DHE-DSS-CAMELLIA128-SHA256", TlsCipherSuite.TLS_DHE_DSS_WITH_CAMELLIA_128_CBC_SHA256),
        ("DHE-DSS-AES256-SHA", TlsCipherSuite.TLS_DHE_DSS_WITH_AES_256_CBC_SHA),
        ("DHE-DSS-CAMELLIA256-SHA", TlsCipherSuite.TLS_DHE_DSS_WITH_CAMELLIA_256_CBC_SHA),
        ("DHE-DSS-AES128-SHA", TlsCipherSuite.TLS_DHE_DSS_WITH_AES_128_CBC_SHA),
        ("DHE-DSS-CAMELLIA128-SHA", TlsCipherSuite.TLS_DHE_DSS_WITH_CAMELLIA_128_CBC_SHA),
        ("DHE-DSS-DES-CBC3-SHA", TlsCipherSuite.TLS_DHE_DSS_WITH_3DES_EDE_CBC_SHA),
    ];

    private static readonly FrozenDictionary<string, TlsCipherSuite> Tls12SuitesByOpenSslName =
        Tls12Names.Concat(Tls12DssNames).ToFrozenDictionary(entry => entry.OpenSslName, entry => entry.Suite, StringComparer.Ordinal);

    // Every TLS 1.3 suite; OpenSSL names them by their IANA names.
    private static readonly FrozenSet<TlsCipherSuite> Tls13Suites = new[]
    {
        TlsCipherSuite.TLS_AES_128_GCM_SHA256,
        TlsCipherSuite.TLS_AES_256_GCM_SHA384,
        TlsCipherSuite.TLS_CHACHA20_POLY1305_SHA256,
        TlsCipherSuite.TLS_AES_128_CCM_SHA256,
        TlsCipherSuite.TLS_AES_128_CCM_8_SHA256,
    }.ToFrozenSet();

    private static readonly char[] EntrySeparators = [':', ',', ' '];

    private const string IanaNamePrefix = "TLS_";

    /// <summary>
    /// Gets the TLS 1.3 suites offered when <c>--tls13-ciphers</c> is absent: OpenSSL's
    /// documented default, in its order.
    /// </summary>
    public static IReadOnlyList<TlsCipherSuite> DefaultTls13Suites { get; } =
    [
        TlsCipherSuite.TLS_AES_256_GCM_SHA384,
        TlsCipherSuite.TLS_CHACHA20_POLY1305_SHA256,
        TlsCipherSuite.TLS_AES_128_GCM_SHA256,
    ];

    /// <summary>
    /// Gets the TLS 1.2-and-below suites offered when <c>--ciphers</c> is absent: every
    /// suite in the OpenSSL name table, in its order.
    /// </summary>
    public static IReadOnlyList<TlsCipherSuite> DefaultTls12Suites { get; } =
        Tls12Names.Select(entry => entry.Suite).ToArray();

    /// <summary>
    /// Chooses the suites for a handshake from the two option values.
    /// </summary>
    /// <param name="ciphers">The <c>--ciphers</c> value, or <see langword="null" />.</param>
    /// <param name="tls13Ciphers">The <c>--tls13-ciphers</c> value, or <see langword="null" />.</param>
    /// <returns>
    /// <see langword="null" /> suites and message when neither option is given, so no policy
    /// is set; the TLS 1.3 suites followed by the TLS 1.2-and-below suites when both lists
    /// have a known entry; otherwise the exit 59 message for the first list that has none.
    /// </returns>
    public static (IReadOnlyList<TlsCipherSuite>? Suites, string? FailureMessage) Select(string? ciphers, string? tls13Ciphers)
    {
        if (ciphers is null && tls13Ciphers is null)
        {
            return (null, null);
        }

        var tls12Suites = ParseListOrDefault(ciphers, DefaultTls12Suites, tls13: false);
        if (tls12Suites.Count == 0)
        {
            return (null, TlsFailureMessages.OpenSslCipherListUnusable(ciphers!));
        }

        var tls13Suites = ParseListOrDefault(tls13Ciphers, DefaultTls13Suites, tls13: true);
        if (tls13Suites.Count == 0)
        {
            return (null, TlsFailureMessages.OpenSslTls13CipherSuiteUnusable(tls13Ciphers!));
        }

        return ([.. tls13Suites, .. tls12Suites], null);
    }

    /// <summary>
    /// The exit 59 message this build gives when it cannot apply the options at all, as on
    /// a platform without <see cref="CipherSuitesPolicy" />: the one for <c>--ciphers</c>
    /// when it is given, otherwise the one for <c>--tls13-ciphers</c>.
    /// </summary>
    /// <param name="ciphers">The <c>--ciphers</c> value, or <see langword="null" />.</param>
    /// <param name="tls13Ciphers">The <c>--tls13-ciphers</c> value, given when <paramref name="ciphers" /> is not.</param>
    /// <returns>The message curl prints.</returns>
    public static string Unapplied(string? ciphers, string? tls13Ciphers) => ciphers is not null
        ? TlsFailureMessages.OpenSslCipherListUnusable(ciphers)
        : TlsFailureMessages.OpenSslTls13CipherSuiteUnusable(tls13Ciphers!);

    /// <summary>
    /// Finds the suite an entry names: its IANA name, the <see cref="TlsCipherSuite" />
    /// member name, or else its OpenSSL name.
    /// </summary>
    /// <param name="entry">One entry of a list.</param>
    /// <returns>The suite, or <see langword="null" /> for a name neither table holds.</returns>
    public static TlsCipherSuite? Find(string entry)
    {
        // Enum.TryParse also takes digits, which name no suite; every member begins TLS_.
        if (entry.StartsWith(IanaNamePrefix, StringComparison.Ordinal)
            && Enum.TryParse<TlsCipherSuite>(entry, ignoreCase: false, out var ianaSuite))
        {
            return ianaSuite;
        }

        return Tls12SuitesByOpenSslName.TryGetValue(entry, out var openSslSuite) ? openSslSuite : null;
    }

    // The option's known suites, or the build's defaults when the option is not given.
    private static IReadOnlyList<TlsCipherSuite> ParseListOrDefault(
        string? value,
        IReadOnlyList<TlsCipherSuite> defaults,
        bool tls13) =>
        value is null ? defaults : ParseList(value, tls13);

    // Keeps the known suites of the one version, in the order given, each once.
    private static List<TlsCipherSuite> ParseList(string value, bool tls13)
    {
        var suites = new List<TlsCipherSuite>();
        foreach (var entry in value.Split(EntrySeparators, StringSplitOptions.RemoveEmptyEntries))
        {
            if (Find(entry) is { } suite && Tls13Suites.Contains(suite) == tls13 && !suites.Contains(suite))
            {
                suites.Add(suite);
            }
        }

        return suites;
    }
}
