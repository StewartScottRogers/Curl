using Libcurl.Misc.Version;

namespace Curl.Config;

/// <summary>
/// What <c>curl-config</c> reports about the build (ADR-0712): libcurl's version and numeric version,
/// its protocols and features, the TLS backend, the install prefix and the CA bundle.
/// </summary>
/// <param name="Version">The libcurl version, such as <c>8.22.0</c>.</param>
/// <param name="VersionNumber">The version as <c>0xXXYYZZ</c>.</param>
/// <param name="Protocols">The protocols as <c>--protocols</c> lists them: upper-case, in <c>curl -V</c>'s order.</param>
/// <param name="Features">The feature names as <c>curl -V</c> spells them.</param>
/// <param name="SslBackends">The TLS backend the platform's curl is built with.</param>
/// <param name="Prefix">The install prefix.</param>
/// <param name="CaBundle">The CA bundle install path; empty, as Curl installs none.</param>
public sealed record CurlConfigBuild(
    string Version,
    int VersionNumber,
    IReadOnlyList<string> Protocols,
    IReadOnlyList<string> Features,
    string SslBackends,
    string Prefix,
    string CaBundle)
{
    /// <summary>
    /// The build <c>curl_version_info</c> describes. Its protocols gain <c>ipfs</c> and <c>ipns</c>
    /// beside <c>http</c>, which the curl tool serves itself and lists in <c>curl -V</c>, as upstream's
    /// configure lists them in <c>curl-config</c>; the TLS backend is <c>Schannel</c> on Windows and
    /// <c>OpenSSL</c> elsewhere, the backends of the platform's reference curl.
    /// </summary>
    /// <param name="info">Libcurl's <c>curl_version_info</c>.</param>
    /// <param name="isWindows">Whether the running system is Windows.</param>
    /// <param name="prefix">The install prefix, see <see cref="PrefixOf"/>.</param>
    /// <returns>The build.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="info"/> is <see langword="null"/>.</exception>
    public static CurlConfigBuild From(CurlVersionInfoData info, bool isWindows, string prefix)
    {
        ArgumentNullException.ThrowIfNull(info);
        return new(info.Version, info.VersionNumber, ProtocolsOf(info.Protocols), info.FeatureNames, isWindows ? "Schannel" : "OpenSSL", prefix, "");
    }

    /// <summary>The install prefix: the folder above the one the executable is in.</summary>
    /// <param name="baseDirectory">The executable's folder, such as <see cref="AppContext.BaseDirectory"/>.</param>
    /// <returns>Its parent folder, or an empty string for a root.</returns>
    public static string PrefixOf(string baseDirectory) =>
        Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(baseDirectory)) ?? "";

    private static string[] ProtocolsOf(IReadOnlyList<string> protocols) =>
        [.. (protocols.Contains("http") ? protocols.Concat(["ipfs", "ipns"]) : protocols)
            .Order(StringComparer.Ordinal)
            .Select(protocol => protocol.ToUpperInvariant())];
}
