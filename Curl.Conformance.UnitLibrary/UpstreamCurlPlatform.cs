namespace Curl.Conformance;

/// <summary>
/// What a case may depend on about the platform Curl runs on: the features Curl reports, which
/// decide <c>&lt;features&gt;</c> and <c>%if</c>, and the null device <c>%DEV_NULL</c> names. Curl
/// matches the Schannel build of curl on Windows and the OpenSSL build elsewhere.
/// </summary>
/// <remarks>
/// A feature is listed only when Curl has it: the protocol names it registers, <c>SSL</c> and the
/// TLS backend it matches, <c>large_file</c>, and <c>local-http</c> (the harness's server is at
/// <c>127.0.0.1</c>); <c>win32</c> on Windows, as <c>runtests.pl</c> sets it there. Features Curl
/// lacks, such as <c>Debug</c>, <c>cookies</c>, <c>proxy</c>, <c>libz</c> and <c>IPv6</c>, stay off,
/// so the cases that need them are skipped rather than counted as failures.
/// </remarks>
public sealed class UpstreamCurlPlatform
{
    private static readonly string[] CommonFeatures =
        ["dict", "file", "gopher", "gophers", "http", "https", "mqtt", "telnet", "tftp", "SSL", "large_file", "local-http"];

    private UpstreamCurlPlatform(IEnumerable<string> platformFeatures, string nullDevice)
    {
        Features = CommonFeatures.Concat(platformFeatures).ToHashSet(StringComparer.Ordinal);
        NullDevice = nullDevice;
    }

    /// <summary>Windows, where Curl matches curl's Schannel build.</summary>
    public static UpstreamCurlPlatform Windows { get; } = new(["win32", "Schannel"], "NUL");

    /// <summary>Linux and macOS, where Curl matches curl's OpenSSL build.</summary>
    public static UpstreamCurlPlatform Unix { get; } = new(["OpenSSL"], "/dev/null");

    /// <summary>The features Curl reports on this platform, case-sensitive as upstream spells them.</summary>
    public IReadOnlySet<string> Features { get; }

    /// <summary>The value of <c>%DEV_NULL</c>.</summary>
    public string NullDevice { get; }
}
