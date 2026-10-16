namespace Curl.Api.EngineContracts;

/// <summary>
/// What <c>curl_version_info</c> reports about the engine (<c>curl_version_info_data</c>): the
/// version, the features, the protocols and the TLS backend (ADR-0509). Features are by name, as
/// <c>feature_names</c> lists them; <c>Libcurl</c> derives the <c>CURL_VERSION_*</c> bitmask.
/// Every default describes libcurl 8.22.0 with nothing built in; an engine fills in what it has.
/// </summary>
public sealed record CurlVersionInfo
{
    /// <summary><c>version</c>: the version text. Default <c>8.22.0</c>, the targeted libcurl.</summary>
    public string Version { get; init; } = "8.22.0";

    /// <summary><c>version_num</c>: the version as <c>0xXXYYZZ</c>. Default <c>0x081600</c> (8.22.0).</summary>
    public int VersionNumber { get; init; } = 0x081600;

    /// <summary><c>host</c>: the platform the engine was built for. Default empty.</summary>
    public string Host { get; init; } = "";

    /// <summary><c>feature_names</c>: the names of the features built in, such as <c>SSL</c> or <c>IPv6</c>. Default empty.</summary>
    public IReadOnlyList<string> Features { get; init; } = [];

    /// <summary><c>protocols</c>: the lower-case schemes supported, such as <c>http</c>. Default empty.</summary>
    public IReadOnlyList<string> Protocols { get; init; } = [];

    /// <summary><c>ssl_version</c>: the TLS backend's name and version, such as <c>Schannel</c>. Default <see langword="null"/> (no TLS).</summary>
    public string? TlsBackend { get; init; }
}
