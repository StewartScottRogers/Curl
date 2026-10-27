using System.Collections.Frozen;
using System.Globalization;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Applies <c>--ciphers</c> as the Schannel build of curl does (ADR-0011, ADR-0816): the
/// list is ALG_ID entries split on <c>:</c>, each a non-zero decimal value or a
/// <c>CALG_*</c> name matched exactly, or a <c>USE_STRONG_CRYPTO</c> /
/// <c>SCH_USE_STRONG_CRYPTO</c> flag. Any other entry is exit 59; a list whose algorithms
/// include none Schannel accepts is exit 35 from AcquireCredentialsHandle; and a list
/// takes curl onto Schannel's legacy credential, which does not offer TLS 1.3.
/// </summary>
internal static class SchannelAlgorithmList
{
    // curl's NUM_CIPHERS: set_ssl_ciphers stops reading the list after this many algorithms.
    private const int MaximumAlgorithms = 47;

    // curl's get_alg_id_by_name as its Windows build compiles it: CALG_NULLCIPHER and the
    // CALG_THIRDPARTY_* names are not in it (measured, curl 8.22.0 Schannel).
    private static readonly FrozenDictionary<string, int> AlgorithmIds = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["CALG_MD2"] = 0x8001,
        ["CALG_MD4"] = 0x8002,
        ["CALG_MD5"] = 0x8003,
        ["CALG_SHA"] = 0x8004,
        ["CALG_SHA1"] = 0x8004,
        ["CALG_MAC"] = 0x8005,
        ["CALG_RSA_SIGN"] = 0x2400,
        ["CALG_DSS_SIGN"] = 0x2200,
        ["CALG_NO_SIGN"] = 0x2000,
        ["CALG_RSA_KEYX"] = 0xA400,
        ["CALG_DES"] = 0x6601,
        ["CALG_3DES_112"] = 0x6609,
        ["CALG_3DES"] = 0x6603,
        ["CALG_DESX"] = 0x6604,
        ["CALG_RC2"] = 0x6602,
        ["CALG_RC4"] = 0x6801,
        ["CALG_SEAL"] = 0x6802,
        ["CALG_DH_SF"] = 0xAA01,
        ["CALG_DH_EPHEM"] = 0xAA02,
        ["CALG_AGREEDKEY_ANY"] = 0xAA03,
        ["CALG_HUGHES_MD5"] = 0xA003,
        ["CALG_SKIPJACK"] = 0x660A,
        ["CALG_TEK"] = 0x660B,
        ["CALG_CYLINK_MEK"] = 0x660C,
        ["CALG_SSL3_SHAMD5"] = 0x8008,
        ["CALG_SSL3_MASTER"] = 0x4C01,
        ["CALG_SCHANNEL_MASTER_HASH"] = 0x4C02,
        ["CALG_SCHANNEL_MAC_KEY"] = 0x4C03,
        ["CALG_SCHANNEL_ENC_KEY"] = 0x4C07,
        ["CALG_PCT1_MASTER"] = 0x4C04,
        ["CALG_SSL2_MASTER"] = 0x4C05,
        ["CALG_TLS1_MASTER"] = 0x4C06,
        ["CALG_RC5"] = 0x660D,
        ["CALG_HMAC"] = 0x8009,
        ["CALG_TLS1PRF"] = 0x800A,
        ["CALG_HASH_REPLACE_OWF"] = 0x800B,
        ["CALG_AES_128"] = 0x660E,
        ["CALG_AES_192"] = 0x660F,
        ["CALG_AES_256"] = 0x6610,
        ["CALG_AES"] = 0x6611,
        ["CALG_SHA_256"] = 0x800C,
        ["CALG_SHA_384"] = 0x800D,
        ["CALG_SHA_512"] = 0x800E,
        ["CALG_ECDH"] = 0xAA05,
        ["CALG_ECDH_EPHEM"] = 0xAE06,
        ["CALG_ECMQV"] = 0xA001,
        ["CALG_ECDSA"] = 0x2203,
    }.ToFrozenDictionary(StringComparer.Ordinal);

    // The algorithms AcquireCredentialsHandle accepted on Windows 11 (measured each name,
    // curl 8.22.0 Schannel); every other algorithm alone is SEC_E_ALGORITHM_MISMATCH.
    private static readonly FrozenSet<int> AcceptedAlgorithmIds =
        [0x8004, 0x2400, 0xA400, 0x660E, 0x6610, 0x800C, 0x800D, 0xAE06, 0x2203];

    private static readonly string[] StrongCryptoFlags = ["USE_STRONG_CRYPTO", "SCH_USE_STRONG_CRYPTO"];

    /// <summary>
    /// Gets the failure the Schannel build gives for a <c>--ciphers</c> list before it
    /// connects, or <see langword="null" /> when the list is absent or Schannel takes it.
    /// </summary>
    /// <param name="ciphers">The <c>--ciphers</c> value, or <see langword="null" />.</param>
    /// <returns>Exit 59 for an entry naming no algorithm, exit 35 for a list of algorithms
    /// Schannel accepts none of, otherwise <see langword="null" />.</returns>
    internal static ConnectResult? Refusal(string? ciphers)
    {
        if (ciphers is null)
        {
            return null;
        }

        var algorithmIds = AlgorithmIdsOf(ciphers);
        return algorithmIds is null ? ConnectResult.Failed(CurlExitCode.SslCipher, TlsFailureMessages.SchannelCipherListRefused)
            : algorithmIds.Count == 0 || algorithmIds.Exists(AcceptedAlgorithmIds.Contains) ? null
            : ConnectResult.Failed(CurlExitCode.SslConnectError, TlsFailureMessages.SchannelAlgorithmMismatch);
    }

    /// <summary>
    /// Gets the options the Schannel build connects with: a <c>--ciphers</c> list whose range
    /// reaches TLS 1.3 from below it stops at TLS 1.2, since curl's legacy credential does
    /// not offer TLS 1.3 (measured: a TLS 1.3-only server is SEC_E_UNSUPPORTED_FUNCTION).
    /// </summary>
    /// <param name="options">The options as given.</param>
    /// <param name="matchesSchannelBuild">Whether the provider matches the Schannel build.</param>
    /// <returns>The options with the ceiling applied, or <paramref name="options" />.</returns>
    internal static TlsClientOptions WithLegacyCredentialRange(TlsClientOptions options, bool matchesSchannelBuild) =>
        matchesSchannelBuild && options.Ciphers is not null && ReachesTls13FromBelow(options)
            ? options with { MaximumVersion = TlsVersion.Tls12 }
            : options;

    private static bool ReachesTls13FromBelow(TlsClientOptions options) =>
        options.MinimumVersion is not TlsVersion.Tls13
        && options.MaximumVersion is TlsVersion.SystemDefault or TlsVersion.Tls13;

    // curl's set_ssl_ciphers: the algorithms the list names, or null when an entry names
    // neither an algorithm nor the strong-crypto flag. curl stops at the end of the string,
    // so an empty entry after the last colon is never read.
    private static List<int>? AlgorithmIdsOf(string ciphers)
    {
        var entries = ciphers.Split(':');
        var read = entries[^1].Length == 0 ? entries.Length - 1 : entries.Length;
        List<int> algorithmIds = [];
        for (var index = 0; index < read && algorithmIds.Count < MaximumAlgorithms; index++)
        {
            if (!TryAdd(algorithmIds, entries[index]))
            {
                return null;
            }
        }

        return algorithmIds;
    }

    // Adds the entry's algorithm; false when it names neither an algorithm nor the flag.
    private static bool TryAdd(List<int> algorithmIds, string entry)
    {
        var algorithmId = AlgorithmIdOf(entry);
        if (algorithmId != 0)
        {
            algorithmIds.Add(algorithmId);
        }

        return algorithmId != 0 || Array.Exists(StrongCryptoFlags, flag => entry.StartsWith(flag, StringComparison.Ordinal));
    }

    // A leading run of decimal digits that is a non-zero int is that ALG_ID; otherwise the
    // entry is looked up by name, and 0 means it names none.
    private static int AlgorithmIdOf(string entry)
    {
        var digits = entry.Length - entry.AsSpan().TrimStart("0123456789").Length;
        return digits > 0
            && int.TryParse(entry.AsSpan(0, digits), NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            && value != 0
                ? value
                : AlgorithmIds.GetValueOrDefault(entry);
    }
}
