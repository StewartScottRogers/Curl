namespace Curl.Engine;

/// <summary>
/// The features a run behaves as built without, as upstream's test suite builds libcurl without
/// them (ADR-0746, BL-2984). <see cref="None" />, the default, hides nothing: neither the shipped
/// curl tool nor <c>Libcurl</c> ever sets another value; the conformance harness sets one per case.
/// </summary>
[Flags]
internal enum EngineFeatureMask
{
    /// <summary>Every feature is built in.</summary>
    None = 0,

    /// <summary>Proxy support, which a build with <c>CURL_DISABLE_PROXY</c> lacks: a set proxy answers <c>CURLE_NOT_BUILT_IN</c>.</summary>
    Proxy = 1,

    /// <summary>IPv6, which a build with <c>ENABLE_IPV6</c> off lacks (BL-2982).</summary>
    Ipv6 = 2,

    /// <summary>International domain names, which a build without libidn2 or WinIDN lacks (BL-2983).</summary>
    Idn = 4,
}
