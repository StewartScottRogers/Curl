using System.Collections.Frozen;
using System.Net.Security;

namespace Curl.Networking;

/// <summary>
/// Turns <c>--ciphers</c> and <c>--tls13-ciphers</c> into the one list of suites
/// <see cref="CipherSuitesPolicy" /> takes, as ADR-0011 decides for the OpenSSL build of
/// curl: <c>--ciphers</c> is an OpenSSL cipher string, which <see cref="OpenSslCipherString" />
/// expands (ADR-0817); <c>--tls13-ciphers</c> entries are split on <c>:</c>, <c>,</c> and spaces, matched case-sensitively
/// against the IANA name and then the OpenSSL name, and unknown entries are dropped; a list
/// with no known entry is exit 59.
/// </summary>
internal static class OpenSslCipherSuites
{
    // OpenSSL's names for the TLS 1.2-and-below suites TlsCipherSuite has, in the order of
    // OpenSSL 3.5.5's ALL list (ADR-0817). The DHE-DSS ones are its COMPLEMENTOFDEFAULT, so
    // DEFAULT, offered when --ciphers is absent, is the others in this order.
    private static readonly (string OpenSslName, TlsCipherSuite Suite)[] Tls12Names =
    [
        ("ECDHE-ECDSA-AES256-GCM-SHA384", TlsCipherSuite.TLS_ECDHE_ECDSA_WITH_AES_256_GCM_SHA384),
        ("ECDHE-RSA-AES256-GCM-SHA384", TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384),
        ("DHE-DSS-AES256-GCM-SHA384", TlsCipherSuite.TLS_DHE_DSS_WITH_AES_256_GCM_SHA384),
        ("DHE-RSA-AES256-GCM-SHA384", TlsCipherSuite.TLS_DHE_RSA_WITH_AES_256_GCM_SHA384),
        ("ECDHE-ECDSA-CHACHA20-POLY1305", TlsCipherSuite.TLS_ECDHE_ECDSA_WITH_CHACHA20_POLY1305_SHA256),
        ("ECDHE-RSA-CHACHA20-POLY1305", TlsCipherSuite.TLS_ECDHE_RSA_WITH_CHACHA20_POLY1305_SHA256),
        ("DHE-RSA-CHACHA20-POLY1305", TlsCipherSuite.TLS_DHE_RSA_WITH_CHACHA20_POLY1305_SHA256),
        ("DHE-DSS-ARIA256-GCM-SHA384", TlsCipherSuite.TLS_DHE_DSS_WITH_ARIA_256_GCM_SHA384),
        ("ECDHE-ECDSA-AES128-GCM-SHA256", TlsCipherSuite.TLS_ECDHE_ECDSA_WITH_AES_128_GCM_SHA256),
        ("ECDHE-RSA-AES128-GCM-SHA256", TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256),
        ("DHE-DSS-AES128-GCM-SHA256", TlsCipherSuite.TLS_DHE_DSS_WITH_AES_128_GCM_SHA256),
        ("DHE-RSA-AES128-GCM-SHA256", TlsCipherSuite.TLS_DHE_RSA_WITH_AES_128_GCM_SHA256),
        ("DHE-DSS-ARIA128-GCM-SHA256", TlsCipherSuite.TLS_DHE_DSS_WITH_ARIA_128_GCM_SHA256),
        ("ECDHE-ECDSA-AES256-SHA384", TlsCipherSuite.TLS_ECDHE_ECDSA_WITH_AES_256_CBC_SHA384),
        ("ECDHE-RSA-AES256-SHA384", TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_256_CBC_SHA384),
        ("DHE-RSA-AES256-SHA256", TlsCipherSuite.TLS_DHE_RSA_WITH_AES_256_CBC_SHA256),
        ("DHE-DSS-AES256-SHA256", TlsCipherSuite.TLS_DHE_DSS_WITH_AES_256_CBC_SHA256),
        ("DHE-DSS-CAMELLIA256-SHA256", TlsCipherSuite.TLS_DHE_DSS_WITH_CAMELLIA_256_CBC_SHA256),
        ("ECDHE-ECDSA-AES128-SHA256", TlsCipherSuite.TLS_ECDHE_ECDSA_WITH_AES_128_CBC_SHA256),
        ("ECDHE-RSA-AES128-SHA256", TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_128_CBC_SHA256),
        ("DHE-RSA-AES128-SHA256", TlsCipherSuite.TLS_DHE_RSA_WITH_AES_128_CBC_SHA256),
        ("DHE-DSS-AES128-SHA256", TlsCipherSuite.TLS_DHE_DSS_WITH_AES_128_CBC_SHA256),
        ("DHE-DSS-CAMELLIA128-SHA256", TlsCipherSuite.TLS_DHE_DSS_WITH_CAMELLIA_128_CBC_SHA256),
        ("ECDHE-ECDSA-AES256-SHA", TlsCipherSuite.TLS_ECDHE_ECDSA_WITH_AES_256_CBC_SHA),
        ("ECDHE-RSA-AES256-SHA", TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_256_CBC_SHA),
        ("DHE-RSA-AES256-SHA", TlsCipherSuite.TLS_DHE_RSA_WITH_AES_256_CBC_SHA),
        ("DHE-DSS-AES256-SHA", TlsCipherSuite.TLS_DHE_DSS_WITH_AES_256_CBC_SHA),
        ("DHE-DSS-CAMELLIA256-SHA", TlsCipherSuite.TLS_DHE_DSS_WITH_CAMELLIA_256_CBC_SHA),
        ("ECDHE-ECDSA-AES128-SHA", TlsCipherSuite.TLS_ECDHE_ECDSA_WITH_AES_128_CBC_SHA),
        ("ECDHE-RSA-AES128-SHA", TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_128_CBC_SHA),
        ("DHE-RSA-AES128-SHA", TlsCipherSuite.TLS_DHE_RSA_WITH_AES_128_CBC_SHA),
        ("DHE-DSS-AES128-SHA", TlsCipherSuite.TLS_DHE_DSS_WITH_AES_128_CBC_SHA),
        ("DHE-DSS-CAMELLIA128-SHA", TlsCipherSuite.TLS_DHE_DSS_WITH_CAMELLIA_128_CBC_SHA),
        ("AES256-GCM-SHA384", TlsCipherSuite.TLS_RSA_WITH_AES_256_GCM_SHA384),
        ("AES128-GCM-SHA256", TlsCipherSuite.TLS_RSA_WITH_AES_128_GCM_SHA256),
        ("AES256-SHA256", TlsCipherSuite.TLS_RSA_WITH_AES_256_CBC_SHA256),
        ("AES128-SHA256", TlsCipherSuite.TLS_RSA_WITH_AES_128_CBC_SHA256),
        ("AES256-SHA", TlsCipherSuite.TLS_RSA_WITH_AES_256_CBC_SHA),
        ("AES128-SHA", TlsCipherSuite.TLS_RSA_WITH_AES_128_CBC_SHA),
    ];

    // A suite no alias of OpenSSL 3.5.5 selects (3DES is disabled there), kept for its name.
    private static readonly (string OpenSslName, TlsCipherSuite Suite)[] Tls12NamedOnlyNames =
    [
        ("DHE-DSS-DES-CBC3-SHA", TlsCipherSuite.TLS_DHE_DSS_WITH_3DES_EDE_CBC_SHA),
    ];

    private static readonly FrozenDictionary<string, TlsCipherSuite> Tls12SuitesByOpenSslName =
        Tls12Names.Concat(Tls12NamedOnlyNames).ToFrozenDictionary(entry => entry.OpenSslName, entry => entry.Suite, StringComparer.Ordinal);

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
    /// Gets the TLS 1.2-and-below suites offered when <c>--ciphers</c> is absent or
    /// starts with <c>DEFAULT</c>: OpenSSL's DEFAULT list, every suite in the OpenSSL name
    /// table but the DHE-DSS ones, in its order.
    /// </summary>
    public static IReadOnlyList<TlsCipherSuite> DefaultTls12Suites { get; } =
        Tls12Names.Where(entry => !entry.OpenSslName.Contains("-DSS-", StringComparison.Ordinal)).Select(entry => entry.Suite).ToArray();

    /// <summary>
    /// Gets the suites OpenSSL's cipher-string aliases select from, with their OpenSSL
    /// names, in the order of OpenSSL's ALL list.
    /// </summary>
    internal static IReadOnlyList<(string OpenSslName, TlsCipherSuite Suite)> AliasedTls12Names => Tls12Names;

    /// <summary>
    /// Gets every TLS 1.2-and-below suite the table names, the ones no alias selects included.
    /// </summary>
    internal static IEnumerable<(string OpenSslName, TlsCipherSuite Suite)> NamedTls12Suites => Tls12Names.Concat(Tls12NamedOnlyNames);

    /// <summary>
    /// Tells whether a suite is a TLS 1.3 one, which <c>--ciphers</c> never selects.
    /// </summary>
    /// <param name="suite">The suite.</param>
    /// <returns><see langword="true" /> for a TLS 1.3 suite.</returns>
    internal static bool IsTls13(TlsCipherSuite suite) => Tls13Suites.Contains(suite);

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

        var tls12Suites = Tls12SuitesOf(ciphers);
        if (tls12Suites is not { Count: > 0 })
        {
            return (null, TlsFailureMessages.OpenSslCipherListUnusable(ciphers!));
        }

        var tls13Suites = Tls13SuitesOf(tls13Ciphers);
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

    // The suites --ciphers selects, the default list when it is absent, or null for a
    // value OpenSSL refuses.
    private static IReadOnlyList<TlsCipherSuite>? Tls12SuitesOf(string? ciphers) =>
        ciphers is null ? DefaultTls12Suites : OpenSslCipherString.Expand(ciphers);

    // The suites --tls13-ciphers names, or the default list when it is absent.
    private static IReadOnlyList<TlsCipherSuite> Tls13SuitesOf(string? tls13Ciphers) =>
        tls13Ciphers is null ? DefaultTls13Suites : ParseTls13List(tls13Ciphers);

    // Keeps the known TLS 1.3 suites, in the order given, each once.
    private static List<TlsCipherSuite> ParseTls13List(string value)
    {
        var suites = new List<TlsCipherSuite>();
        foreach (var entry in value.Split(EntrySeparators, StringSplitOptions.RemoveEmptyEntries))
        {
            if (Find(entry) is { } suite && Tls13Suites.Contains(suite) && !suites.Contains(suite))
            {
                suites.Add(suite);
            }
        }

        return suites;
    }
}
