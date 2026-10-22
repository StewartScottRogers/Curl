using System.Collections.Frozen;
using Curl.Api.EngineContracts;
using Libcurl.Global;

namespace Libcurl.Misc.Version;

/// <summary>
/// <c>curl_version</c> and <c>curl_version_info</c> over the library's engine's
/// <see cref="ICurlEngine.VersionInfo"/> (ADR-0509, ADR-0591). Both answer after
/// <c>curl_global_cleanup</c> too, as libcurl's answer needs no initialisation.
/// </summary>
public static class CurlVersion
{
    // curl 8.22.0's lib/version.c features table: each feature name and the CURL_VERSION_* bits it sets.
    // "Debug" sets CURLDEBUG too (the same bit as DEBUG since 8.19.0), and CharConv and TrackMemory are gone.
    private static readonly FrozenDictionary<string, CurlVersionFeatures> FeatureBits = new Dictionary<string, CurlVersionFeatures>(StringComparer.Ordinal)
    {
        ["alt-svc"] = CurlVersionFeatures.AltSvc,
        ["AppleSecTrust"] = CurlVersionFeatures.None,
        ["AsynchDNS"] = CurlVersionFeatures.AsynchDns,
        ["asyn-rr"] = CurlVersionFeatures.None,
        ["brotli"] = CurlVersionFeatures.Brotli,
        ["Debug"] = CurlVersionFeatures.Debug | CurlVersionFeatures.CurlDebug,
        ["ECH"] = CurlVersionFeatures.None,
        ["gsasl"] = CurlVersionFeatures.Gsasl,
        ["GSS-API"] = CurlVersionFeatures.GssApi,
        ["HSTS"] = CurlVersionFeatures.Hsts,
        ["HTTP2"] = CurlVersionFeatures.Http2,
        ["HTTP3"] = CurlVersionFeatures.Http3,
        ["HTTPS-proxy"] = CurlVersionFeatures.HttpsProxy,
        ["HTTPSIG"] = CurlVersionFeatures.None,
        ["HTTPSRR"] = CurlVersionFeatures.None,
        ["IDN"] = CurlVersionFeatures.Idn,
        ["IPv6"] = CurlVersionFeatures.IPv6,
        ["Kerberos"] = CurlVersionFeatures.Kerberos5,
        ["Largefile"] = CurlVersionFeatures.LargeFile,
        ["libz"] = CurlVersionFeatures.Libz,
        ["MultiSSL"] = CurlVersionFeatures.MultiSsl,
        ["NativeCA"] = CurlVersionFeatures.None,
        ["NTLM"] = CurlVersionFeatures.Ntlm,
        ["NTLM_WB"] = CurlVersionFeatures.NtlmWinbind,
        ["proxy-HTTP3"] = CurlVersionFeatures.None,
        ["PSL"] = CurlVersionFeatures.Psl,
        ["SPNEGO"] = CurlVersionFeatures.Spnego,
        ["SSL"] = CurlVersionFeatures.Ssl,
        ["SSLS-EXPORT"] = CurlVersionFeatures.None,
        ["SSPI"] = CurlVersionFeatures.Sspi,
        ["threadsafe"] = CurlVersionFeatures.ThreadSafe,
        ["TLS-SRP"] = CurlVersionFeatures.TlsAuthSrp,
        ["Unicode"] = CurlVersionFeatures.Unicode,
        ["UnixSockets"] = CurlVersionFeatures.UnixSockets,
        ["zstd"] = CurlVersionFeatures.Zstd,
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>
    /// Every feature name curl 8.22.0's <c>lib/version.c</c> features table lists, with a bit or without one
    /// (<c>ECH</c>, <c>HTTPSRR</c>, <c>SSLS-EXPORT</c> and the like), in no particular order. Upstream's
    /// <c>test1177.pl</c> checks these against <c>curl_version_info.3</c>.
    /// </summary>
    public static IReadOnlyCollection<string> FeatureNames => FeatureBits.Keys;

    /// <summary>
    /// <c>curl_version</c>: <c>libcurl/</c> and the engine's version, then a space and its TLS
    /// backend when it has one - the text <c>curl -V</c>'s first line ends with, such as
    /// <c>libcurl/8.22.0 Schannel</c>.
    /// </summary>
    /// <param name="library">The library whose engine is asked.</param>
    /// <returns>The version text.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="library"/> is <see langword="null"/>.</exception>
    public static string Text(CurlLibrary library)
    {
        ArgumentNullException.ThrowIfNull(library);
        CurlVersionInfo info = library.Engine.VersionInfo;
        return info.TlsBackend is null ? $"libcurl/{info.Version}" : $"libcurl/{info.Version} {info.TlsBackend}";
    }

    /// <summary>
    /// <c>curl_version_info</c>: the engine's version, host, protocols, TLS backend and features, with
    /// the <c>CURL_VERSION_*</c> bits of the feature names. Like libcurl, it ignores
    /// <paramref name="age"/> and answers every field of <see cref="CurlVersionAge.Now"/>, which a
    /// caller asking for an older age reads only the start of.
    /// </summary>
    /// <param name="library">The library whose engine is asked.</param>
    /// <param name="age">The age the caller expects (<c>CURLVERSION_NOW</c> for the newest); unused, as in libcurl.</param>
    /// <returns>The version information, aged <see cref="CurlVersionAge.Now"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="library"/> is <see langword="null"/>.</exception>
    public static CurlVersionInfoData Info(CurlLibrary library, CurlVersionAge age = CurlVersionAge.Now)
    {
        ArgumentNullException.ThrowIfNull(library);
        _ = age;
        CurlVersionInfo info = library.Engine.VersionInfo;
        return new CurlVersionInfoData
        {
            Version = info.Version,
            VersionNumber = info.VersionNumber,
            Host = info.Host,
            Features = FeaturesOf(info.Features),
            SslVersion = info.TlsBackend,
            Protocols = info.Protocols,
            FeatureNames = info.Features,
        };
    }

    /// <summary>
    /// The <c>CURL_VERSION_*</c> bit <c>lib/version.c</c> sets for the feature named
    /// <paramref name="featureName"/>, matched exactly; <see cref="CurlVersionFeatures.None"/> for a
    /// feature with no bit (<c>ECH</c>, <c>HTTPSRR</c>, <c>SSLS-EXPORT</c>) or a name libcurl does not list.
    /// </summary>
    /// <param name="featureName">The feature name, as <c>feature_names</c> and <c>curl -V</c> list it.</param>
    /// <returns>The feature's bit, or <see cref="CurlVersionFeatures.None"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="featureName"/> is <see langword="null"/>.</exception>
    public static CurlVersionFeatures FeatureBit(string featureName) =>
        FeatureBits.GetValueOrDefault(featureName);

    // Combines the bits of every feature name the engine lists into curl_version_info_data.features.
    private static CurlVersionFeatures FeaturesOf(IReadOnlyList<string> featureNames)
    {
        CurlVersionFeatures features = CurlVersionFeatures.None;
        foreach (string name in featureNames)
        {
            features |= FeatureBit(name);
        }

        return features;
    }
}
