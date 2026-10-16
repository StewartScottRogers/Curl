namespace Curl.Conformance;

/// <summary>
/// What a case may depend on about the platform Curl runs on: the features Curl reports, which
/// decide <c>&lt;features&gt;</c> and <c>%if</c>, and the null device <c>%DEV_NULL</c> names. Curl
/// matches the Schannel build of curl on Windows and the OpenSSL build elsewhere.
/// </summary>
/// <remarks>
/// <para>
/// A feature is listed when Curl has it, by the name upstream's <c>runtests.pl</c> gives it: the
/// protocols the harness can serve (<c>ws</c>, <c>wss</c> and <c>ipfs</c> among them, and <c>sftp</c>
/// and <c>scp</c>, which Curl serves and <c>UpstreamSshServer</c> emulates); every
/// feature on Curl's <c>curl -V</c> <c>Features:</c> line, except that <c>ECH</c> is listed because
/// Curl still serves <c>--ech</c>, though the line does not name it (ADR-0520), which keeps the cases
/// that need it measured; <c>HTTP2</c> is reported as
/// <c>http/2</c> and <c>h2c</c>; <c>crypto</c>, which upstream derives from
/// <c>NTLM</c>, <c>Kerberos</c> and <c>SPNEGO</c>; the tool features a curl build has unless it
/// disables them (<c>cookies</c>, <c>proxy</c>, <c>Mime</c>, <c>manual</c>, <c>DoH</c>,
/// <c>digest</c>, <c>aws</c>, <c>netrc</c>, <c>verbose-strings</c>, <c>large-time</c>,
/// <c>large-size</c>, <c>sha512-256</c>, <c>SSLpinning</c> and <c>httpsig</c>); <c>headers-api</c>, since
/// Curl writes <c>-w %header{name}</c> and <c>%{header_json}</c>, which curl writes only with libcurl's
/// headers API (BL-2264); <c>large_file</c>;
/// <c>local-http</c> (the harness's server is at <c>127.0.0.1</c>); <c>win32</c> on Windows, as
/// <c>runtests.pl</c> sets it there; the TLS backend; <c>xattr</c> off Windows, where curl
/// cannot write extended attributes; <c>http/2</c> and <c>h2c</c> off Windows only, since the
/// Schannel build Curl matches there has no <c>HTTP2</c> and refuses <c>--http2</c> (BL-2230).
/// <c>http/3</c> is listed on no platform: Curl refuses <c>--http3</c> and <c>--http3-only</c> on
/// every reference build, none of which has a QUIC library (ADR-0540), so upstream's <c>runtests.pl</c>
/// skips the cases that need it, as it does for the reference curl (BL-2370, GF-0094).
/// </para>
/// <para>
/// These stay off, so the cases that need them are skipped rather than counted as failures:
/// libcurl's build and API features, which a command-line tool does not have (<c>Debug</c>,
/// <c>TrackMemory</c>, <c>unittest</c>, <c>form-api</c>, <c>wakeup</c>,
/// <c>shuffle-dns</c>, <c>override-dns</c>, <c>threadsafe</c>, <c>getrlimit</c>,
/// <c>--libcurl</c>); <c>ftp</c> and the other protocols the harness has no server for; features
/// absent from Curl's <c>curl -V</c> (<c>SSPI</c>, <c>Unicode</c>, <c>MultiSSL</c>,
/// <c>ssl-sessions</c>, <c>threaded-resolver</c>); and <c>codeset-utf8</c>, which depends on the
/// locale of the machine running the tests.
/// </para>
/// </remarks>
public sealed class UpstreamCurlPlatform
{
    private static readonly string[] CommonFeatures =
    [
        "dict", "file", "gopher", "gophers", "http", "https", "ipfs", "mqtt", "mqtts", "scp", "sftp", "telnet", "tftp", "ws", "wss",
        "alt-svc", "AsynchDNS", "brotli", "ECH", "GSS-API", "HSTS", "HTTPS-proxy",
        "HTTPSRR", "IDN", "IPv6", "Kerberos", "Largefile", "libz", "NTLM", "PSL", "SPNEGO", "SSL",
        "UnixSockets", "zstd",
        "crypto", "cookies", "proxy", "Mime", "manual", "DoH", "digest", "aws", "netrc", "verbose-strings",
        "large-time", "large-size", "sha512-256", "SSLpinning",
        "large_file", "local-http", "httpsig", "headers-api",
    ];

    private UpstreamCurlPlatform(IEnumerable<string> platformFeatures, string nullDevice, string operatingSystemName)
    {
        Features = CommonFeatures.Concat(platformFeatures).ToHashSet(StringComparer.Ordinal);
        NullDevice = nullDevice;
        OperatingSystemName = operatingSystemName;
    }

    private UpstreamCurlPlatform(UpstreamCurlPlatform basis, IEnumerable<string> extraFeatures)
    {
        Features = basis.Features.Concat(extraFeatures).ToHashSet(StringComparer.Ordinal);
        NullDevice = basis.NullDevice;
        OperatingSystemName = basis.OperatingSystemName;
    }

    /// <summary>Windows, where Curl matches curl's Schannel build.</summary>
    public static UpstreamCurlPlatform Windows { get; } = new(["win32", "Schannel"], "NUL", "MSWin32");

    /// <summary>Linux, where Curl matches curl's OpenSSL build.</summary>
    public static UpstreamCurlPlatform Unix { get; } = new(["OpenSSL", "xattr", "http/2", "h2c"], "/dev/null", "linux");

    /// <summary>macOS, where Curl matches curl's OpenSSL build as on Linux; only Perl's name for it differs.</summary>
    public static UpstreamCurlPlatform MacOS { get; } = new(["OpenSSL", "xattr", "http/2", "h2c"], "/dev/null", "darwin");

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
        return new UpstreamCurlPlatform(this, extraFeatures);
    }

    /// <summary>The value of <c>%DEV_NULL</c>.</summary>
    public string NullDevice { get; }

    /// <summary>Perl's <c>$^O</c> on this platform, which precheck one-liners such as test2072's test.</summary>
    public string OperatingSystemName { get; }
}
