namespace Curl.Conformance;

/// <summary>
/// What a case may depend on about the platform Curl runs on: the features Curl reports, which
/// decide <c>&lt;features&gt;</c> and <c>%if</c>, and the null device <c>%DEV_NULL</c> names. Curl
/// matches the Schannel build of curl on Windows and the OpenSSL build elsewhere.
/// </summary>
/// <remarks>
/// <para>
/// A feature is listed when Curl has it, by the name upstream's <c>runtests.pl</c> gives it: the
/// protocols the harness can serve (<c>ws</c>, <c>wss</c> and <c>ipfs</c> among them); every
/// feature on Curl's <c>curl -V</c> <c>Features:</c> line, with <c>HTTP2</c> as <c>http/2</c> and
/// <c>h2c</c> and <c>HTTP3</c> as <c>http/3</c>; <c>crypto</c>, which upstream derives from
/// <c>NTLM</c>, <c>Kerberos</c> and <c>SPNEGO</c>; the tool features a curl build has unless it
/// disables them (<c>cookies</c>, <c>proxy</c>, <c>Mime</c>, <c>manual</c>, <c>DoH</c>,
/// <c>digest</c>, <c>aws</c>, <c>netrc</c>, <c>verbose-strings</c>, <c>large-time</c>,
/// <c>large-size</c>, <c>sha512-256</c> and <c>SSLpinning</c>); <c>large_file</c>;
/// <c>local-http</c> (the harness's server is at <c>127.0.0.1</c>); <c>win32</c> on Windows, as
/// <c>runtests.pl</c> sets it there; the TLS backend; and <c>xattr</c> off Windows, where curl
/// cannot write extended attributes.
/// </para>
/// <para>
/// These stay off, so the cases that need them are skipped rather than counted as failures:
/// libcurl's build and API features, which a command-line tool does not have (<c>Debug</c>,
/// <c>TrackMemory</c>, <c>unittest</c>, <c>headers-api</c>, <c>form-api</c>, <c>wakeup</c>,
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
        "dict", "file", "gopher", "gophers", "http", "https", "ipfs", "mqtt", "telnet", "tftp", "ws", "wss",
        "alt-svc", "AsynchDNS", "brotli", "ECH", "GSS-API", "HSTS", "http/2", "h2c", "http/3", "HTTPS-proxy",
        "HTTPSRR", "IDN", "IPv6", "Kerberos", "Largefile", "libz", "NTLM", "PSL", "SPNEGO", "SSL", "TLS-SRP",
        "UnixSockets", "zstd",
        "crypto", "cookies", "proxy", "Mime", "manual", "DoH", "digest", "aws", "netrc", "verbose-strings",
        "large-time", "large-size", "sha512-256", "SSLpinning",
        "large_file", "local-http",
    ];

    private UpstreamCurlPlatform(IEnumerable<string> platformFeatures, string nullDevice, string operatingSystemName)
    {
        Features = CommonFeatures.Concat(platformFeatures).ToHashSet(StringComparer.Ordinal);
        NullDevice = nullDevice;
        OperatingSystemName = operatingSystemName;
    }

    /// <summary>Windows, where Curl matches curl's Schannel build.</summary>
    public static UpstreamCurlPlatform Windows { get; } = new(["win32", "Schannel"], "NUL", "MSWin32");

    /// <summary>Linux, where Curl matches curl's OpenSSL build.</summary>
    public static UpstreamCurlPlatform Unix { get; } = new(["OpenSSL", "xattr"], "/dev/null", "linux");

    /// <summary>macOS, where Curl matches curl's OpenSSL build as on Linux; only Perl's name for it differs.</summary>
    public static UpstreamCurlPlatform MacOS { get; } = new(["OpenSSL", "xattr"], "/dev/null", "darwin");

    /// <summary>The features Curl reports on this platform, case-sensitive as upstream spells them.</summary>
    public IReadOnlySet<string> Features { get; }

    /// <summary>The value of <c>%DEV_NULL</c>.</summary>
    public string NullDevice { get; }

    /// <summary>Perl's <c>$^O</c> on this platform, which precheck one-liners such as test2072's test.</summary>
    public string OperatingSystemName { get; }
}
