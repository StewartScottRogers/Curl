namespace Curl.Conformance;

/// <summary>
/// What a case may depend on about the platform Curl runs on: the features Curl reports, which
/// decide <c>&lt;features&gt;</c> and <c>%if</c>, and the null device <c>%DEV_NULL</c> names. Curl
/// matches the Schannel build of curl on Windows and the OpenSSL build elsewhere.
/// </summary>
/// <remarks>
/// <para>
/// A feature is listed when Curl has it, by the name upstream's <c>runtests.pl</c> gives it: the
/// protocols the harness can serve (<c>ftp</c>, <c>ws</c>, <c>wss</c> and <c>ipfs</c> among them; <c>pop3</c>, <c>smtp</c> and
/// <c>imap</c>, which its mail stand-ins serve (BL-2741); and <c>sftp</c>
/// and <c>scp</c>, which Curl serves and <c>UpstreamSshServer</c> emulates); every
/// feature on Curl's <c>curl -V</c> <c>Features:</c> line, except that <c>ECH</c> is listed because
/// Curl still serves <c>--ech</c>, though the line does not name it (ADR-0520), which keeps the cases
/// that need it measured; <c>HTTP2</c> is reported as
/// <c>http/2</c> and <c>h2c</c>; <c>crypto</c>, which upstream derives from
/// <c>NTLM</c>, <c>Kerberos</c> and <c>SPNEGO</c>; the tool features a curl build has unless it
/// disables them (<c>cookies</c>, <c>proxy</c>, <c>Mime</c>, <c>manual</c>, <c>DoH</c>,
/// <c>digest</c>, <c>aws</c>, <c>netrc</c>, <c>verbose-strings</c>, <c>large-time</c>,
/// <c>large-size</c>, <c>sha512-256</c>, <c>SSLpinning</c>, <c>httpsig</c> and <c>--libcurl</c>, since Curl
/// writes the C program <c>--libcurl</c> names (BL-2862)); <c>headers-api</c>, since
/// Curl writes <c>-w %header{name}</c> and <c>%{header_json}</c>, which curl writes only with libcurl's
/// headers API (BL-2264); <c>form-api</c>, since <c>Libcurl</c>, which runs the ported libtest
/// programs, has <c>CURLOPT_HTTPPOST</c> (BL-2593); <c>wakeup</c>, since that <c>Libcurl</c> has <c>curl_multi_wakeup</c> (BL-2661, ADR-0690);
/// <c>threadsafe</c>, which Curl's <c>Features:</c> line and <c>curl_version_info</c> name, and
/// <c>threaded-resolver</c>, which <c>runtests.pl</c> derives from <c>AsynchDNS</c> without <c>c-ares</c>
/// (ADR-0695, BL-2700); <c>codeset-utf8</c>, which <c>runtests.pl</c> derives from a UTF-8 locale on
/// the machine running the tests: Curl reads URLs, mail addresses and <c>-w</c> text as UTF-8 whatever
/// the locale, so the harness stands in for a UTF-8 locale on every platform (BL-2861); <c>large_file</c>;
/// <c>local-http</c> (the harness's server is at <c>127.0.0.1</c>); <c>win32</c> on Windows, as
/// <c>runtests.pl</c> sets it there; the TLS backend; <c>xattr</c> off Windows, where curl
/// cannot write extended attributes; <c>http/2</c> and <c>h2c</c> on every platform (BL-3268): the
/// Schannel build Curl once matched had no <c>HTTP2</c> and refused <c>--http2</c> (BL-2230), but Curl runs
/// <c>--http2</c> on Windows since ADR-0871, and BL-3268 turned them on there.
/// <c>http/3</c> is listed on no platform: Curl refuses <c>--http3</c> and <c>--http3-only</c> on
/// every reference build, none of which has a QUIC library (ADR-0540), so upstream's <c>runtests.pl</c>
/// skips the cases that need it, as it does for the reference curl (BL-2370, GF-0094).
/// </para>
/// <para>
/// These stay off, so the cases that need them are skipped rather than counted as failures:
/// libcurl's build and API features, which a command-line tool does not have (<c>Debug</c>,
/// <c>TrackMemory</c>, <c>unittest</c>,
/// <c>shuffle-dns</c>, <c>override-dns</c>, <c>getrlimit</c>); the other protocols the harness has no server for; features
/// absent from Curl's <c>curl -V</c> (<c>SSPI</c>, <c>Unicode</c>, <c>MultiSSL</c>, <c>c-ares</c>), and
/// <c>ssl-sessions</c> on Windows, where the Schannel build has no <c>SSLS-EXPORT</c>; off Windows
/// <c>runtests.pl</c> derives <c>ssl-sessions</c> from <c>SSLS-EXPORT</c>, and so does this list (ADR-0737, BL-2858).
/// </para>
/// </remarks>
public sealed class UpstreamCurlPlatform
{
    private UpstreamCurlPlatform(IEnumerable<string> platformFeatures, string nullDevice, string operatingSystemName)
    {
        Features = UpstreamPlatformFeatureTable.Common.Concat(platformFeatures).ToHashSet(StringComparer.Ordinal);
        NullDevice = nullDevice;
        OperatingSystemName = operatingSystemName;
    }

    private UpstreamCurlPlatform(UpstreamCurlPlatform basis, IEnumerable<string> extraFeatures, IEnumerable<string> hiddenFeatures)
    {
        Features = basis.Features.Concat(extraFeatures).Except(hiddenFeatures, StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);
        NullDevice = basis.NullDevice;
        OperatingSystemName = basis.OperatingSystemName;
    }

    /// <summary>Windows, where Curl matches curl's Schannel build.</summary>
    public static UpstreamCurlPlatform Windows { get; } = new(UpstreamPlatformFeatureTable.Windows, "NUL", "MSWin32");

    /// <summary>Linux, where Curl matches curl's OpenSSL build.</summary>
    public static UpstreamCurlPlatform Unix { get; } = new(UpstreamPlatformFeatureTable.Unix, "/dev/null", "linux");

    /// <summary>macOS, where Curl matches curl's OpenSSL build as on Linux; only Perl's name for it differs.</summary>
    public static UpstreamCurlPlatform MacOS { get; } = new(UpstreamPlatformFeatureTable.Unix, "/dev/null", "darwin");

    /// <summary>Every platform Curl's conformance run stands for: Windows, Linux and macOS.</summary>
    public static IReadOnlyList<UpstreamCurlPlatform> All { get; } = [Windows, Unix, MacOS];

    /// <summary>The features Curl reports on this platform, case-sensitive as upstream spells them.</summary>
    public IReadOnlySet<string> Features { get; }

    /// <summary>
    /// Returns this platform with more features, for a harness that supplies one the platform lacks
    /// (such as <c>Debug</c> for a case that needs the signing clock fixed); this platform is unchanged.
    /// </summary>
    /// <param name="extraFeatures">The features to add, spelled as upstream spells them.</param>
    /// <returns>A platform with this one's features, null device and operating system name, and <paramref name="extraFeatures"/>.</returns>
    public UpstreamCurlPlatform WithFeatures(IEnumerable<string> extraFeatures)
    {
        ArgumentNullException.ThrowIfNull(extraFeatures);
        return new UpstreamCurlPlatform(this, extraFeatures, []);
    }

    /// <summary>
    /// Returns this platform without some features, for a case run with Curl's feature mask hiding them, as
    /// upstream's suite runs a case naming <c>!proxy</c> on a build without proxy support (ADR-0746, BL-2856);
    /// this platform is unchanged.
    /// </summary>
    /// <param name="hiddenFeatures">The features to take off, spelled as upstream spells them.</param>
    /// <returns>A platform with this one's features but <paramref name="hiddenFeatures"/>, null device and operating system name.</returns>
    public UpstreamCurlPlatform WithoutFeatures(IEnumerable<string> hiddenFeatures)
    {
        ArgumentNullException.ThrowIfNull(hiddenFeatures);
        return new UpstreamCurlPlatform(this, [], hiddenFeatures);
    }

    /// <summary>The value of <c>%DEV_NULL</c>.</summary>
    public string NullDevice { get; }

    /// <summary>Perl's <c>$^O</c> on this platform, which precheck one-liners such as test2072's test.</summary>
    public string OperatingSystemName { get; }
}
