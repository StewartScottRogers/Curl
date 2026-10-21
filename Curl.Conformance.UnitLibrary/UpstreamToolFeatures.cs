namespace Curl.Conformance;

/// <summary>The tool features a curl build has unless it disables them (<c>--libcurl</c> among them, since Curl writes the C program, BL-2862), and <c>crypto</c>, which upstream derives from NTLM, Kerberos and SPNEGO.</summary>
internal static class UpstreamToolFeatures
{
    public static readonly string[] Names =
    [
        "crypto",
        "cookies",
        "proxy",
        "Mime",
        "manual",
        "DoH",
        "digest",
        "aws",
        "netrc",
        "verbose-strings",
        "large-time",
        "large-size",
        "sha512-256",
        "SSLpinning",
        "httpsig",
        "--libcurl",
    ];
}
